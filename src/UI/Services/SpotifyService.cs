using System.Net.Http.Headers;
using System.Text.Json;
using UI.Models;

namespace UI.Services;

public sealed class SpotifyService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SpotifyService> _logger;

    public SpotifyService(HttpClient httpClient, ILogger<SpotifyService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.BaseAddress = new Uri("https://api.spotify.com/v1/");
    }

    public async Task<List<SpotifyPlaylist>> GetUserPlaylistsAsync(string accessToken)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "Bearer", accessToken);
            HttpResponseMessage response = await _httpClient.GetAsync(requestUri: "me/playlists");
            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync();
            UserPlaylistsResponse? playlistsResponse = JsonSerializer.Deserialize<UserPlaylistsResponse>(responseBody);

            return playlistsResponse?.Items ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user playlists");
            return [];
        }
    }

    public async Task<List<SpotifyTrack>> GetPlaylistTracksAsync(string accessToken, string playlistId)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "Bearer", accessToken);
            HttpResponseMessage response = await _httpClient.GetAsync(requestUri: $"playlists/{playlistId}/tracks");
            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync();
            PlaylistTrackResponse? trackResponse = JsonSerializer.Deserialize<PlaylistTrackResponse>(responseBody);

            List<SpotifyTrack> tracks = trackResponse?.Items.Select(i => i.Track).ToList() ?? [];

            foreach (SpotifyTrack? track in tracks)
            {
                if (track.Artists.Count > 0)
                {
                    string artistId = track.Artists[0].Id;
                    track.Genre = await GetArtistPrimaryGenreAsync(accessToken, artistId);
                }
            }

            return tracks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                             "Error getting playlist tracks for playlist {PlaylistId}",
                             playlistId);
            return [];
        }
    }

    private async Task<string> GetArtistPrimaryGenreAsync(string accessToken, string artistId)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "Bearer", accessToken);
            HttpResponseMessage response = await _httpClient.GetAsync(requestUri: $"artists/{artistId}");
            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync();
            SpotifyArtist? artist = JsonSerializer.Deserialize<SpotifyArtist>(responseBody);

            return artist?.Genres.FirstOrDefault() ?? "Unknown";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                             "Error getting artist genre for artist {ArtistId}",
                             artistId);
            return "Unknown";
        }
    }

    public async Task<Dictionary<string, List<SpotifyTrack>>> SortTracksByGenreAsync(string accessToken, string playlistId)
    {
        List<SpotifyTrack> tracks = await GetPlaylistTracksAsync(accessToken, playlistId);

        return tracks
               .GroupBy(t => string.IsNullOrEmpty(t.Genre) ? "Unknown" : t.Genre)
               .ToDictionary(g => g.Key, g => g.ToList());
    }
}