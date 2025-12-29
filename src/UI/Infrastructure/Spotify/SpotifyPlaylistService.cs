using System.Diagnostics;
using UI.Features.Shared.Domain;
using UI.Infrastructure.Observability;
using UI.Infrastructure.Spotify.Models;

namespace UI.Infrastructure.Spotify;

/// <summary>
/// Service for managing Spotify playlists.
/// </summary>
public sealed class SpotifyPlaylistService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SpotifyPlaylistService> _logger;
    private readonly SpotifyUserService _userService;

    public SpotifyPlaylistService(
        HttpClient httpClient,
        ILogger<SpotifyPlaylistService> logger,
        SpotifyUserService userService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _userService = userService;

        _httpClient.BaseAddress = new Uri("https://api.spotify.com/v1/");
    }

    public async Task<List<Playlist>> GetUserPlaylistsAsync()
    {
        _logger.LogDebug("Fetching user playlists from Spotify API");

        try
        {
            UserPlaylistsResponse playlistsResponse = await _httpClient.GetFromJsonAsync<UserPlaylistsResponse>(requestUri: "me/playlists") ?? throw new InvalidOperationException("Response can not be null");

            List<Playlist> playlists = playlistsResponse.Items.MapToDomain().ToList();

            _logger.LogInformation("Successfully fetched {PlaylistCount} playlists", playlists.Count);

            Activity.Current?.SetTag("playlist.count", playlists.Count);

            return playlists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user playlists");

            Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);

            throw;
        }
    }

    public async Task<Playlist> GetPlaylistAsync(string playlistId)
    {
        Activity.Current?.SetTag("playlist.id", playlistId);

        _logger.LogDebug("Fetching playlist details for {PlaylistId}", playlistId);

        try
        {
            string requestUri = $"playlists/{playlistId}";

            SpotifyPlaylist playlistDto = await _httpClient.GetFromJsonAsync<SpotifyPlaylist>(requestUri) ??
                                          throw new InvalidOperationException("Response can not be null");

            Playlist playlist = playlistDto.MapToDomain();

            _logger.LogInformation("Fetched playlist {PlaylistName} ({PlaylistId})",
                                   playlist.Name,
                                   playlistId);

            Activity.Current?.SetTag("playlist.name", playlist.Name);

            return playlist;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                             "Error getting playlist details for playlist {PlaylistId}",
                             playlistId);

            Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);

            throw;
        }
    }

    public async Task<Playlist> CreatePlaylistAsync(string name, string? description = null)
    {
        _logger.LogDebug("Creating playlist {PlaylistName}", name);

        try
        {
            SpotifyUser user = await _userService.GetCurrentUserAsync();
            string requestUri = $"users/{user.Id}/playlists";

            CreatePlaylistRequest request = new()
            {
                Name = name,
                Description = description,
                Public = false
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(requestUri, request);
            response.EnsureSuccessStatusCode();

            SpotifyPlaylist playlistDto = await response.Content.ReadFromJsonAsync<SpotifyPlaylist>() ?? throw new InvalidOperationException("Response can not be null");

            Playlist playlist = playlistDto.MapToDomain();

            _logger.LogInformation("Created playlist {PlaylistName} ({PlaylistId})", playlist.Name, playlist.Id);

            ActivityTagsCollection tags = new()
            {
                ["playlist.id"] = playlist.Id,
                ["playlist.track_count"] = playlist.TrackCount
            };
            ActivityEvent @event = new(name: "Playlist created.", DateTimeOffset.UtcNow, tags);
            Activity.Current?.AddEvent(@event);

            return playlist;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating playlist {PlaylistName}", name);

            Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);

            throw;
        }
    }

    public async Task AddTracksToPlaylistAsync(string playlistId, IEnumerable<string> trackUris)
    {
        List<string> uriList = trackUris.ToList();
        _logger.LogDebug("Adding {TrackCount} tracks to playlist {PlaylistId}", uriList.Count, playlistId);

        try
        {
            string requestUri = $"playlists/{playlistId}/tracks";

            AddTracksRequest request = new()
            {
                Uris = uriList
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(requestUri, request);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Added {TrackCount} tracks to playlist {PlaylistId}", uriList.Count, playlistId);

            ActivityTagsCollection tags = new()
            {
                ["playlist.added_tracks_count"] = uriList.Count
            };
            ActivityEvent @event = new(name: "Added tracks to playlist.", DateTimeOffset.UtcNow, tags);
            Activity.Current?.AddEvent(@event);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding tracks to playlist {PlaylistId}", playlistId);

            Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);

            throw;
        }
    }

    public async Task<Playlist?> FindPlaylistByNameAsync(string name)
    {
        _logger.LogDebug("Searching for playlist by name: {PlaylistName}", name);

        List<Playlist> playlists = await GetUserPlaylistsAsync();
        Playlist? playlist = playlists.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (playlist != null)
        {
            _logger.LogInformation("Found existing playlist {PlaylistName} ({PlaylistId})", playlist.Name, playlist.Id);

            ActivityTagsCollection tags = new()
            {
                ["playlist.existing_tracks_count"] = playlist.TrackCount
            };
            ActivityEvent @event = new(name: "Playlist found.", DateTimeOffset.UtcNow, tags);
            Activity.Current?.AddEvent(@event);
        }
        else
        {
            _logger.LogInformation("Playlist {PlaylistName} not found", name);

            ActivityEvent @event = new(name: "Playlist not found.", DateTimeOffset.UtcNow);
            Activity.Current?.AddEvent(@event);
        }

        return playlist;
    }

    public async Task<Playlist> GetOrCreatePlaylistAsync(string name, string? description = null)
    {
        Activity.Current?.SetTag("playlist.name", name);

        if (!string.IsNullOrEmpty(description))
        {
            Activity.Current?.SetTag("playlist.description", name);
        }

        _logger.LogDebug("Getting or creating playlist: {PlaylistName}", name);

        Playlist? existingPlaylist = await FindPlaylistByNameAsync(name);

        if (existingPlaylist != null)
            return existingPlaylist;

        Playlist newPlaylist = await CreatePlaylistAsync(name, description);

        return newPlaylist;
    }
}