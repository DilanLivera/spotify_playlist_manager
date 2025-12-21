using System.Diagnostics;
using UI.Infrastructure.Observability;

namespace UI.Infrastructure.ReccoBeats;

/// <summary>
/// Service for interacting with ReccoBeats API for audio features.
/// </summary>
public sealed class ReccoBeatsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ReccoBeatsService> _logger;

    public ReccoBeatsService(HttpClient httpClient, ILogger<ReccoBeatsService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.BaseAddress = new Uri("https://api.reccobeats.com/");
    }

    /// <summary>
    /// Gets audio features for a track from ReccoBeats API.
    /// First looks up the track by Spotify ID, then fetches audio features using ReccoBeats internal ID.
    /// Implements rate limiting best practices with retry logic for 429 responses.
    /// </summary>
    public async Task<ReccoBeatsAudioFeatures?> GetAudioFeaturesAsync(string spotifyTrackId, CancellationToken cancellationToken)
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("ReccoBeats_GetAudioFeatures");
        activity?.SetTag("spotify.track.id", spotifyTrackId);

        _logger.LogDebug("Fetching audio features for Spotify track {SpotifyTrackId} from ReccoBeats", spotifyTrackId);

        try
        {
            // Step 1: Look up the track using Spotify ID to get ReccoBeats internal ID
            string? reccoBeatsTrackId = await LookupTrackIdAsync(spotifyTrackId, cancellationToken);

            if (string.IsNullOrEmpty(reccoBeatsTrackId))
            {
                _logger.LogWarning("Track {SpotifyTrackId} not found in ReccoBeats database", spotifyTrackId);
                activity?.SetTag("track.not_found", true);

                return null;
            }

            activity?.SetTag("reccobeats.track.id", reccoBeatsTrackId);

            // Step 2: Fetch audio features using ReccoBeats internal ID
            return await FetchAudioFeaturesWithRetryAsync(reccoBeatsTrackId, spotifyTrackId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Audio features request cancelled for track {SpotifyTrackId}", spotifyTrackId);

            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching audio features for track {SpotifyTrackId}", spotifyTrackId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            return null;
        }
    }

    /// <summary>
    /// Looks up a track by Spotify ID and returns the ReccoBeats internal ID.
    /// </summary>
    private async Task<string?> LookupTrackIdAsync(string spotifyTrackId, CancellationToken cancellationToken)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"v1/track?ids={spotifyTrackId}", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            ReccoBeatsTrackLookupResponse? result = await response.Content.ReadFromJsonAsync<ReccoBeatsTrackLookupResponse>(cancellationToken);

            return result?.Content.FirstOrDefault()?.Id;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error looking up track {SpotifyTrackId}", spotifyTrackId);

            return null;
        }
    }

    /// <summary>
    /// Fetches audio features with retry logic for rate limiting.
    /// </summary>
    private async Task<ReccoBeatsAudioFeatures?> FetchAudioFeaturesWithRetryAsync(string reccoBeatsTrackId, string spotifyTrackId, CancellationToken cancellationToken)
    {
        int maxRetries = 3;
        int retryCount = 0;

        while (retryCount <= maxRetries)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                HttpResponseMessage response = await _httpClient.GetAsync($"v1/track/{reccoBeatsTrackId}/audio-features", cancellationToken);

                // Handle rate limiting (429 Too Many Requests)
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    retryCount++;

                    if (retryCount > maxRetries)
                    {
                        _logger.LogWarning("Max retries reached for track {SpotifyTrackId} due to rate limiting", spotifyTrackId);

                        return null;
                    }

                    // Check for Retry-After header
                    TimeSpan retryAfter = TimeSpan.FromSeconds(2); // Default to 2 seconds
                    if (response.Headers.TryGetValues("Retry-After", out IEnumerable<string>? retryAfterValues))
                    {
                        string? retryAfterValue = retryAfterValues.FirstOrDefault();
                        if (!string.IsNullOrEmpty(retryAfterValue) && int.TryParse(retryAfterValue, out int seconds))
                        {
                            retryAfter = TimeSpan.FromSeconds(seconds);
                        }
                    }

                    _logger.LogInformation("Rate limited for track {SpotifyTrackId}, waiting {RetryAfter} seconds before retry {RetryCount}/{MaxRetries}",
                                           spotifyTrackId,
                                           retryAfter.TotalSeconds,
                                           retryCount,
                                           maxRetries);

                    await Task.Delay(retryAfter, cancellationToken);

                    continue;
                }

                // Handle 404 Not Found (track not available)
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Audio features not found for track {SpotifyTrackId} in ReccoBeats", spotifyTrackId);

                    return null;
                }

                response.EnsureSuccessStatusCode();

                ReccoBeatsAudioFeatures? result = await response.Content.ReadFromJsonAsync<ReccoBeatsAudioFeatures>(cancellationToken);

                if (result != null)
                {
                    _logger.LogDebug("Successfully fetched audio features for track {SpotifyTrackId}", spotifyTrackId);

                    return result;
                }

                _logger.LogWarning("ReccoBeats API returned null response for track {SpotifyTrackId}", spotifyTrackId);

                return null;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "HTTP error fetching audio features for track {SpotifyTrackId}", spotifyTrackId);

                return null;
            }
        }

        return null;
    }
}