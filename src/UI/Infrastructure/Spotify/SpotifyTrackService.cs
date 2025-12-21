using System.Diagnostics;
using UI.Features.Shared.Domain;
using UI.Infrastructure.Observability;
using UI.Infrastructure.ReccoBeats;
using UI.Infrastructure.Spotify.Models;

namespace UI.Infrastructure.Spotify;

/// <summary>
/// Service for fetching Spotify tracks.
/// </summary>
public sealed class SpotifyTrackService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SpotifyTrackService> _logger;
    private readonly SpotifyTrackEnricher _trackEnricher;

    public SpotifyTrackService(
        HttpClient httpClient,
        ILogger<SpotifyTrackService> logger,
        SpotifyTrackEnricher trackEnricher)
    {
        _httpClient = httpClient;
        _logger = logger;
        _trackEnricher = trackEnricher;

        _httpClient.BaseAddress = new Uri("https://api.spotify.com/v1/");
    }

    public async Task<IReadOnlyList<Track>> GetPlaylistTracksAsync(string playlistId, int offset, int limit, CancellationToken cancellationToken)
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("GetPlaylistTracks");
        activity?.SetTag("playlist.id", playlistId);
        activity?.SetTag("playlist.offset", offset);
        activity?.SetTag("playlist.limit", limit);

        _logger.LogDebug("Fetching tracks for playlist {PlaylistId} (offset: {Offset}, limit: {Limit})",
            playlistId, offset, limit);

        try
        {
            string requestUri = $"playlists/{playlistId}/tracks?offset={offset}&limit={limit}";

            PlaylistTrackResponse trackResponse = await _httpClient.GetFromJsonAsync<PlaylistTrackResponse>(requestUri, cancellationToken) ?? throw new InvalidOperationException("Response can not be null");

            SpotifyTrack[] dtoTracks = trackResponse.Items
                                                 .Select(i => i.Track)
                                                 .ToArray();

            // Enrich tracks with genres and get audio features for mapping
            await _trackEnricher.EnrichTracksAsync(dtoTracks, cancellationToken);
            Dictionary<string, ReccoBeatsAudioFeatures> audioFeatures = await _trackEnricher.GetAudioFeaturesAsync(
                dtoTracks.Select(t => t.Id).ToArray(), 
                cancellationToken);

            Track[] tracks = dtoTracks.MapToDomain(audioFeatures).ToArray();

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
}

