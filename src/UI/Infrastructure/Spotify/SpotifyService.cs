using System.Diagnostics;
using System.Net.Http.Headers;
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

    public async Task<List<SpotifyPlaylist>> GetUserPlaylistsAsync()
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("GetUserPlaylists");

        _logger.LogDebug("Fetching user playlists from Spotify API");

        try
        {
            Func<Task<List<SpotifyPlaylist>>> apiCall = async () =>
            {
                UserPlaylistsResponse playlistsResponse = await _httpClient.GetFromJsonAsync<UserPlaylistsResponse>(requestUri: "me/playlists") ?? throw new InvalidOperationException("Response can not be null");

                return playlistsResponse.Items;
            };

            List<SpotifyPlaylist> playlists = await ExecuteWithTokenRefreshAsync(apiCall);

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

    public async Task<IReadOnlyList<SpotifyTrack>> GetPlaylistTracksAsync(string playlistId) => await GetPlaylistTracksAsync(playlistId, offset: 0, limit: 100);

    public async Task<IReadOnlyList<SpotifyTrack>> GetPlaylistTracksAsync(string playlistId, int offset, int limit)
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("GetPlaylistTracks");
        activity?.SetTag("playlist.id", playlistId);
        activity?.SetTag("playlist.offset", offset);
        activity?.SetTag("playlist.limit", limit);

        _logger.LogDebug("Fetching tracks for playlist {PlaylistId} (offset: {Offset}, limit: {Limit})",
            playlistId, offset, limit);

        try
        {
            Func<Task<SpotifyTrack[]>> apiCall = async () =>
            {
                string requestUri = $"playlists/{playlistId}/tracks?offset={offset}&limit={limit}";

                PlaylistTrackResponse trackResponse = await _httpClient.GetFromJsonAsync<PlaylistTrackResponse>(requestUri) ?? throw new InvalidOperationException("Response can not be null");

                SpotifyTrack[] tracks = trackResponse.Items
                                                     .Select(i => i.Track)
                                                     .ToArray();

                foreach (SpotifyTrack track in tracks)
                {
                    if (track.Artists.Count > 0)
                    {
                        string artistId = track.Artists[0].Id;
                        track.Genre = await GetArtistPrimaryGenreAsync(artistId);
                    }
                }

                return tracks;
            };

            SpotifyTrack[] tracks = await ExecuteWithTokenRefreshAsync(apiCall);

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

    public async Task<SpotifyPlaylist> GetPlaylistAsync(string playlistId)
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("GetPlaylist");
        activity?.SetTag("playlist.id", playlistId);

        _logger.LogDebug("Fetching playlist details for {PlaylistId}", playlistId);

        try
        {
            Func<Task<SpotifyPlaylist>> apiCall = async () =>
            {
                string requestUri = $"playlists/{playlistId}";

                SpotifyPlaylist playlist = await _httpClient.GetFromJsonAsync<SpotifyPlaylist>(requestUri) ??
                                           throw new InvalidOperationException("Response can not be null");

                return playlist;
            };

            SpotifyPlaylist playlist = await ExecuteWithTokenRefreshAsync(apiCall);

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

    public async Task<Dictionary<string, List<SpotifyTrack>>> SortTracksByGenreAsync(string playlistId)
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("SortTracksByGenre");
        activity?.SetTag("playlist.id", playlistId);

        _logger.LogDebug("Sorting tracks by genre for playlist {PlaylistId}", playlistId);

        IReadOnlyList<SpotifyTrack> tracks = await GetPlaylistTracksAsync(playlistId);

        Dictionary<string, List<SpotifyTrack>> sortedTracks = tracks
            .GroupBy(t => string.IsNullOrEmpty(t.Genre) ? "unknown" : t.Genre)
            .ToDictionary(g => g.Key, g => g.ToList());

        _logger.LogInformation("Sorted {TrackCount} tracks into {GenreCount} genres for playlist {PlaylistId}",
            tracks.Count, sortedTracks.Count, playlistId);
        activity?.SetTag("genre.count", sortedTracks.Count);

        return sortedTracks;
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