using System.Net.Http.Headers;
using UI.Models;

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
        try
        {
            Func<Task<List<SpotifyPlaylist>>> apiCall = async () =>
            {
                UserPlaylistsResponse playlistsResponse = await _httpClient.GetFromJsonAsync<UserPlaylistsResponse>(requestUri: "me/playlists") ?? throw new InvalidOperationException("Response can not be null");

                return playlistsResponse.Items;
            };

            return await ExecuteWithTokenRefreshAsync(apiCall);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user playlists");

            throw;
        }
    }

    public async Task<IReadOnlyList<SpotifyTrack>> GetPlaylistTracksAsync(string playlistId) => await GetPlaylistTracksAsync(playlistId, offset: 0, limit: 100);

    public async Task<IReadOnlyList<SpotifyTrack>> GetPlaylistTracksAsync(string playlistId, int offset, int limit)
    {
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

            return await ExecuteWithTokenRefreshAsync(apiCall);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                             "Error getting playlist tracks for playlist {PlaylistId} (offset: {Offset}, limit: {Limit})",
                             playlistId,
                             offset,
                             limit);
            throw;
        }
    }

    public async Task<SpotifyPlaylist> GetPlaylistAsync(string playlistId)
    {
        try
        {
            Func<Task<SpotifyPlaylist>> apiCall = async () =>
            {
                string requestUri = $"playlists/{playlistId}";

                SpotifyPlaylist playlist = await _httpClient.GetFromJsonAsync<SpotifyPlaylist>(requestUri) ??
                                           throw new InvalidOperationException("Response can not be null");

                return playlist;
            };

            return await ExecuteWithTokenRefreshAsync(apiCall);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                             "Error getting playlist details for playlist {PlaylistId}",
                             playlistId);
            throw;
        }
    }

    public async Task<Dictionary<string, List<SpotifyTrack>>> SortTracksByGenreAsync(string playlistId)
    {
        IReadOnlyList<SpotifyTrack> tracks = await GetPlaylistTracksAsync(playlistId);

        return tracks.GroupBy(t => string.IsNullOrEmpty(t.Genre) ? "unknown" : t.Genre)
                     .ToDictionary(g => g.Key, g => g.ToList());
    }

    private async Task<string> GetArtistPrimaryGenreAsync(string artistId)
    {
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
            _logger.LogError(ex,
                             "Error getting artist genre for artist {ArtistId}",
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
            _logger.LogInformation("Access token expired, attempting to refresh");

            if (await _spotifyAuthService.RefreshTokenAsync())
            {
                UpdateAuthorizationHeader();

                return await apiCall();
            }

            _logger.LogError("Failed to refresh access token");
            throw;
        }
    }

    private void UpdateAuthorizationHeader()
    {
        string accessToken = _spotifyAuthSessionManager.GetAccessToken();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "Bearer", accessToken);
    }
}