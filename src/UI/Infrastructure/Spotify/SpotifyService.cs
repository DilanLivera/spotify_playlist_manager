using System.Diagnostics;
using System.Net.Http.Headers;
using UI.Features.Shared.Domain;
using UI.Infrastructure.Observability;

namespace UI.Infrastructure.Spotify;

public sealed class SpotifyService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SpotifyService> _logger;
    private readonly SpotifyAuthSessionManager _spotifyAuthSessionManager;
    private readonly SpotifyAuthService _spotifyAuthService;

    public SpotifyService(
        HttpClient httpClient,
        ILogger<SpotifyService> logger,
        SpotifyAuthSessionManager spotifyAuthSessionManager,
        SpotifyAuthService spotifyAuthService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _spotifyAuthSessionManager = spotifyAuthSessionManager;
        _spotifyAuthService = spotifyAuthService;

        _httpClient.BaseAddress = new Uri("https://api.spotify.com/v1/");
        UpdateAuthorizationHeader();
    }

    public async Task<List<Playlist>> GetUserPlaylistsAsync()
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("GetUserPlaylists");

        _logger.LogDebug("Fetching user playlists from Spotify API");

        try
        {
            Func<Task<List<Playlist>>> apiCall = async () =>
            {
                UserPlaylistsResponse playlistsResponse = await _httpClient.GetFromJsonAsync<UserPlaylistsResponse>(requestUri: "me/playlists") ?? throw new InvalidOperationException("Response can not be null");

                return playlistsResponse.Items.MapToDomain().ToList();
            };

            List<Playlist> playlists = await ExecuteWithTokenRefreshAsync(apiCall);

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

    public async Task<IReadOnlyList<Track>> GetPlaylistTracksAsync(string playlistId) => await GetPlaylistTracksAsync(playlistId, offset: 0, limit: 100);

    public async Task<IReadOnlyList<Track>> GetPlaylistTracksAsync(string playlistId, int offset, int limit)
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("GetPlaylistTracks");
        activity?.SetTag("playlist.id", playlistId);
        activity?.SetTag("playlist.offset", offset);
        activity?.SetTag("playlist.limit", limit);

        _logger.LogDebug("Fetching tracks for playlist {PlaylistId} (offset: {Offset}, limit: {Limit})",
            playlistId, offset, limit);

        try
        {
            Func<Task<Track[]>> apiCall = async () =>
            {
                string requestUri = $"playlists/{playlistId}/tracks?offset={offset}&limit={limit}";

                PlaylistTrackResponse trackResponse = await _httpClient.GetFromJsonAsync<PlaylistTrackResponse>(requestUri) ?? throw new InvalidOperationException("Response can not be null");

                SpotifyTrack[] dtoTracks = trackResponse.Items
                                                     .Select(i => i.Track)
                                                     .ToArray();

                foreach (SpotifyTrack track in dtoTracks)
                {
                    if (track.Artists.Count > 0)
                    {
                        string artistId = track.Artists[0].Id;
                        track.Genre = await GetArtistPrimaryGenreAsync(artistId);
                    }
                }

                return dtoTracks.MapToDomain().ToArray();
            };

            Track[] tracks = await ExecuteWithTokenRefreshAsync(apiCall);

            _logger.LogInformation("Fetched {TrackCount} tracks for playlist {PlaylistId}",
                tracks.Length, playlistId);
            activity?.SetTag("track.count", tracks.Length);

            return tracks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                             "Error getting playlist tracks for playlist {PlaylistId} (offset: {Offset}, limit: {Limit})",
                             playlistId,
                             offset,
                             limit);
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
            Func<Task<Playlist>> apiCall = async () =>
            {
                string requestUri = $"playlists/{playlistId}";

                SpotifyPlaylist playlistDto = await _httpClient.GetFromJsonAsync<SpotifyPlaylist>(requestUri) ??
                                           throw new InvalidOperationException("Response can not be null");

                return playlistDto.MapToDomain();
            };

            Playlist playlist = await ExecuteWithTokenRefreshAsync(apiCall);

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

    public async Task<SpotifyUser> GetCurrentUserAsync()
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("GetCurrentUser");

        _logger.LogDebug("Fetching current user from Spotify API");

        try
        {
            Func<Task<SpotifyUser>> apiCall = async () =>
            {
                SpotifyUser user = await _httpClient.GetFromJsonAsync<SpotifyUser>(requestUri: "me")
                    ?? throw new InvalidOperationException("Response can not be null");

                return user;
            };

            SpotifyUser user = await ExecuteWithTokenRefreshAsync(apiCall);

            _logger.LogInformation("Fetched current user {UserId}", user.Id);
            activity?.SetTag("user.id", user.Id);

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
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
            Func<Task<Playlist>> apiCall = async () =>
            {
                SpotifyUser user = await GetCurrentUserAsync();
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

                return playlistDto.MapToDomain();
            };

            Playlist playlist = await ExecuteWithTokenRefreshAsync(apiCall);

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
            Func<Task<bool>> apiCall = async () =>
            {
                string requestUri = $"playlists/{playlistId}/tracks";

                AddTracksRequest request = new() { Uris = uriList };

                HttpResponseMessage response = await _httpClient.PostAsJsonAsync(requestUri, request);
                response.EnsureSuccessStatusCode();

                return true;
            };

            await ExecuteWithTokenRefreshAsync(apiCall);

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

    private async Task<string> GetArtistPrimaryGenreAsync(string artistId)
    {
        _logger.LogDebug("Fetching genre for artist {ArtistId}", artistId);

        try
        {
            Func<Task<string>> apiCall = async () =>
            {
                SpotifyArtist artist = await _httpClient.GetFromJsonAsync<SpotifyArtist>(requestUri: $"artists/{artistId}") ?? throw new InvalidOperationException("Response can not be null");

                return artist.Genres.FirstOrDefault() ?? "unknown";
            };

            return await ExecuteWithTokenRefreshAsync(apiCall);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                             "Error getting artist genre for artist {ArtistId}, defaulting to 'unknown'",
                             artistId);

            return "unknown";
        }
    }

    private async Task<T> ExecuteWithTokenRefreshAsync<T>(Func<Task<T>> apiCall)
    {
        try
        {
            return await apiCall();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("Access token expired, attempting to refresh");

            if (await _spotifyAuthService.RefreshTokenAsync())
            {
                UpdateAuthorizationHeader();
                _logger.LogDebug("Token refreshed successfully, retrying API call");

                return await apiCall();
            }

            _logger.LogError("Failed to refresh access token, authentication required");
            throw;
        }
    }

    private void UpdateAuthorizationHeader()
    {
        string accessToken = _spotifyAuthSessionManager.GetAccessToken();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "Bearer", accessToken);
    }
}