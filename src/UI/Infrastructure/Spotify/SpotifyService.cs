using System.Net.Http.Headers;
using UI.Models;

namespace UI.Infrastructure.Spotify;

public sealed class SpotifyService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SpotifyService> _logger;

    public SpotifyService(
        HttpClient httpClient,
        ILogger<SpotifyService> logger,
        SpotifyAuthSessionManager spotifyAuthSessionManager)
    {
        _httpClient = httpClient;
        _logger = logger;

        _httpClient.BaseAddress = new Uri("https://api.spotify.com/v1/");

        string accessToken = spotifyAuthSessionManager.GetAccessToken();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "Bearer", accessToken);
    }

    public async Task<List<SpotifyPlaylist>> GetUserPlaylistsAsync()
    {
        try
        {
            UserPlaylistsResponse playlistsResponse = await _httpClient.GetFromJsonAsync<UserPlaylistsResponse>(requestUri: "me/playlists") ?? throw new InvalidOperationException("Response can not be null");

            return playlistsResponse.Items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user playlists");

            throw;
        }
    }

    public async Task<IReadOnlyList<SpotifyTrack>> GetPlaylistTracksAsync(string playlistId)
    {
        try
        {
            PlaylistTrackResponse trackResponse = await _httpClient.GetFromJsonAsync<PlaylistTrackResponse>(requestUri: $"playlists/{playlistId}/tracks") ?? throw new InvalidOperationException("Response can not be null");

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

            return tracks.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                             "Error getting playlist tracks for playlist {PlaylistId}",
                             playlistId);
            throw;
        }
    }

    private async Task<string> GetArtistPrimaryGenreAsync(string artistId)
    {
        try
        {
            SpotifyArtist artist = await _httpClient.GetFromJsonAsync<SpotifyArtist>(requestUri: $"artists/{artistId}") ?? throw new InvalidOperationException("Response can not be null");

            return artist.Genres.FirstOrDefault() ?? "unknown";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                             "Error getting artist genre for artist {ArtistId}",
                             artistId);

            throw;
        }
    }

    public async Task<Dictionary<string, List<SpotifyTrack>>> SortTracksByGenreAsync(string playlistId)
    {
        IReadOnlyList<SpotifyTrack> tracks = await GetPlaylistTracksAsync(playlistId);

        return tracks.GroupBy(t => string.IsNullOrEmpty(t.Genre) ? "unknown" : t.Genre)
                     .ToDictionary(g => g.Key, g => g.ToList());
    }
}