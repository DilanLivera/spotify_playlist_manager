using System.Diagnostics;
using UI.Infrastructure.Observability;
using UI.Infrastructure.Persistence;
using UI.Infrastructure.ReccoBeats;
using UI.Infrastructure.Spotify.Models;

namespace UI.Infrastructure.Spotify;

/// <summary>
/// Service responsible for enriching Spotify tracks with additional data from external sources.
/// Fetches genres from Spotify Artists API and audio features from ReccoBeats.
/// </summary>
public sealed class SpotifyTrackEnricher
{
    private readonly HttpClient _httpClient;
    private readonly ReccoBeatsService _reccoBeatsService;
    private readonly TrackCacheService _cacheService;
    private readonly ILogger<SpotifyTrackEnricher> _logger;

    public SpotifyTrackEnricher(
        HttpClient httpClient,
        ReccoBeatsService reccoBeatsService,
        TrackCacheService cacheService,
        ILogger<SpotifyTrackEnricher> logger)
    {
        _httpClient = httpClient;
        _reccoBeatsService = reccoBeatsService;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <summary>
    /// Enriches tracks with genre information and audio features.
    /// </summary>
    public async Task EnrichTracksAsync(SpotifyTrack[] tracks, CancellationToken cancellationToken)
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("EnrichTracks");
        activity?.SetTag("track.count", tracks.Length);

        // Extract unique artist IDs
        string[] uniqueArtistIds = tracks.Where(t => t.Artists.Count > 0)
                                         .Select(t => t.Artists[0].Id)
                                         .Distinct()
                                         .ToArray();

        // Bulk fetch genres for all unique artists
        Dictionary<string, string> artistGenres = await GetArtistsGenresAsync(uniqueArtistIds, cancellationToken);

        // Fetch audio features from ReccoBeats for all tracks
        string[] trackIds = tracks.Select(t => t.Id).ToArray();
        Dictionary<string, ReccoBeatsAudioFeatures> audioFeatures = await GetAudioFeaturesFromReccoBeatsAsync(trackIds, cancellationToken);

        // Assign genres to tracks
        foreach (SpotifyTrack track in tracks)
        {
            if (track.Artists.Count > 0)
            {
                string artistId = track.Artists[0].Id;
                track.Genre = artistGenres.GetValueOrDefault(artistId, "unknown");
            }
        }

        _logger.LogInformation("Enriched {TrackCount} tracks with genres and audio features", tracks.Length);
    }

    /// <summary>
    /// Returns audio features dictionary for mapping to domain entities.
    /// </summary>
    public async Task<Dictionary<string, ReccoBeatsAudioFeatures>> GetAudioFeaturesAsync(string[] trackIds, CancellationToken cancellationToken) => await GetAudioFeaturesFromReccoBeatsAsync(trackIds, cancellationToken);

    private async Task<Dictionary<string, ReccoBeatsAudioFeatures>> GetAudioFeaturesFromReccoBeatsAsync(string[] trackIds, CancellationToken cancellationToken)
    {
        if (trackIds.Length == 0)
        {
            return new Dictionary<string, ReccoBeatsAudioFeatures>();
        }

        _logger.LogDebug("Processing audio features for {TrackCount} tracks", trackIds.Length);

        try
        {
            // 1. Check SQLite cache first for all tracks (handles batching internally)
            Dictionary<string, ReccoBeatsAudioFeatures> audioFeaturesMap = await _cacheService.GetCachedFeaturesAsync(trackIds, cancellationToken);

            _logger.LogInformation("Found {CacheCount}/{TotalCount} tracks in SQLite cache", 
                                   audioFeaturesMap.Count, 
                                   trackIds.Length);

            // 2. Identify missing tracks
            string[] missingTrackIds = trackIds.Where(id => !audioFeaturesMap.ContainsKey(id)).ToArray();

            if (missingTrackIds.Length > 0)
            {
                _logger.LogInformation("Fetching {MissingCount} tracks from ReccoBeats API", missingTrackIds.Length);

                // 3. Fetch missing tracks from ReccoBeats API
                // Process tracks sequentially to respect rate limits (as before)
                foreach (string trackId in missingTrackIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    ReccoBeatsAudioFeatures? features = await _reccoBeatsService.GetAudioFeaturesAsync(trackId, cancellationToken);

                    if (features != null)
                    {
                        audioFeaturesMap[trackId] = features;
                        
                        // 4. Save to SQLite cache asynchronously
                        await _cacheService.SaveFeaturesAsync(trackId, features, cancellationToken);
                    }
                }

                _logger.LogInformation("Successfully enriched {SuccessCount}/{TotalCount} tracks (Cache + API)",
                                       audioFeaturesMap.Count,
                                       trackIds.Length);
            }

            return audioFeaturesMap;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Error getting audio features, returning partial results");

            return new Dictionary<string, ReccoBeatsAudioFeatures>();
        }
    }

    private async Task<Dictionary<string, string>> GetArtistsGenresAsync(string[] artistIds, CancellationToken cancellationToken)
    {
        Dictionary<string, string> artistGenres = new();

        if (artistIds.Length == 0)
        {
            return artistGenres;
        }

        _logger.LogDebug("Fetching genres for {ArtistCount} artists in bulk", artistIds.Length);

        try
        {
            // Spotify allows up to 50 artist IDs per request
            const int batchSize = 50;
            List<string[]> batches = artistIds.Select((id, index) => new { id, index })
                                              .GroupBy(x => x.index / batchSize)
                                              .Select(g => g.Select(x => x.id).ToArray())
                                              .ToList();

            foreach (string[] batch in batches)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string ids = string.Join(",", batch);
                string requestUri = $"artists?ids={ids}";

                ArtistsResponse response = await _httpClient.GetFromJsonAsync<ArtistsResponse>(requestUri, cancellationToken) ?? throw new InvalidOperationException("Response can not be null");

                foreach (SpotifyArtist artist in response.Artists)
                {
                    if (artist != null)
                    {
                        artistGenres[artist.Id] = artist.Genres.FirstOrDefault() ?? "unknown";
                    }
                }
            }

            _logger.LogInformation("Fetched genres for {ArtistCount} artists", artistGenres.Count);

            return artistGenres;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Error getting artist genres in bulk, returning partial results");

            return artistGenres;
        }
    }
}