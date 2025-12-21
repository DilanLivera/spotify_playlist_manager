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
        using Activity? activity = ObservabilityExtensions.StartActivity("GetUserPlaylists");

        _logger.LogDebug("Fetching user playlists from Spotify API");

        try
        {
            UserPlaylistsResponse playlistsResponse = await _httpClient.GetFromJsonAsync<UserPlaylistsResponse>(requestUri: "me/playlists") ?? throw new InvalidOperationException("Response can not be null");

            List<Playlist> playlists = playlistsResponse.Items.MapToDomain().ToList();

            _logger.LogInformation("Successfully fetched {PlaylistCount} playlists", playlists.Count);
            activity?.SetTag("playlist.count", playlists.Count);

            return playlists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user playlists");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            throw;
        }
    }

    public async Task<Playlist> GetPlaylistAsync(string playlistId)
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("GetPlaylist");
        activity?.SetTag("playlist.id", playlistId);

        _logger.LogDebug("Fetching playlist details for {PlaylistId}", playlistId);

        try
        {
            string requestUri = $"playlists/{playlistId}";

            SpotifyPlaylist playlistDto = await _httpClient.GetFromJsonAsync<SpotifyPlaylist>(requestUri) ??
                                       throw new InvalidOperationException("Response can not be null");

            Playlist playlist = playlistDto.MapToDomain();

            _logger.LogInformation("Fetched playlist {PlaylistName} ({PlaylistId})",
                playlist.Name, playlistId);
            activity?.SetTag("playlist.name", playlist.Name);

            return playlist;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                             "Error getting playlist details for playlist {PlaylistId}",
                             playlistId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task<Playlist> CreatePlaylistAsync(string name, string? description = null)
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("CreatePlaylist");
        activity?.SetTag("playlist.name", name);

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

            SpotifyPlaylist playlistDto = await response.Content.ReadFromJsonAsync<SpotifyPlaylist>()
                ?? throw new InvalidOperationException("Response can not be null");

            Playlist playlist = playlistDto.MapToDomain();

            _logger.LogInformation("Created playlist {PlaylistName} ({PlaylistId})", playlist.Name, playlist.Id);
            activity?.SetTag("playlist.id", playlist.Id);

            return playlist;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating playlist {PlaylistName}", name);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            throw;
        }
    }

    public async Task AddTracksToPlaylistAsync(string playlistId, IEnumerable<string> trackUris)
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("AddTracksToPlaylist");
        activity?.SetTag("playlist.id", playlistId);

        List<string> uriList = trackUris.ToList();
        _logger.LogDebug("Adding {TrackCount} tracks to playlist {PlaylistId}", uriList.Count, playlistId);

        try
        {
            string requestUri = $"playlists/{playlistId}/tracks";

            AddTracksRequest request = new() { Uris = uriList };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(requestUri, request);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Added {TrackCount} tracks to playlist {PlaylistId}", uriList.Count, playlistId);
            activity?.SetTag("track.count", uriList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding tracks to playlist {PlaylistId}", playlistId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            throw;
        }
    }

    public async Task<Playlist?> FindPlaylistByNameAsync(string name)
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("FindPlaylistByName");
        activity?.SetTag("playlist.name", name);

        _logger.LogDebug("Searching for playlist by name: {PlaylistName}", name);

        List<Playlist> playlists = await GetUserPlaylistsAsync();
        Playlist? playlist = playlists.FirstOrDefault(p =>
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (playlist != null)
        {
            _logger.LogInformation("Found existing playlist {PlaylistName} ({PlaylistId})", playlist.Name, playlist.Id);
            activity?.SetTag("playlist.id", playlist.Id);
            activity?.SetTag("playlist.found", true);
        }
        else
        {
            _logger.LogInformation("Playlist {PlaylistName} not found", name);
            activity?.SetTag("playlist.found", false);
        }

        return playlist;
    }

    public async Task<Playlist> GetOrCreatePlaylistAsync(string name, string? description = null)
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("GetOrCreatePlaylist");
        activity?.SetTag("playlist.name", name);

        _logger.LogDebug("Getting or creating playlist: {PlaylistName}", name);

        Playlist? existingPlaylist = await FindPlaylistByNameAsync(name);

        if (existingPlaylist != null)
        {
            activity?.SetTag("playlist.created", false);
            return existingPlaylist;
        }

        Playlist newPlaylist = await CreatePlaylistAsync(name, description);
        activity?.SetTag("playlist.created", true);

        return newPlaylist;
    }
}

