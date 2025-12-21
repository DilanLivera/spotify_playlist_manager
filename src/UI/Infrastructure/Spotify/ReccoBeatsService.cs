using System.Diagnostics;
using UI.Infrastructure.Observability;

namespace UI.Infrastructure.Spotify;

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
        _httpClient.BaseAddress = new Uri("https://reccobeats.com/api/");
    }

    /// <summary>
    /// Gets audio features for a track from ReccoBeats API.
    /// Implements rate limiting best practices with retry logic for 429 responses.
    /// </summary>
    public async Task<ReccoBeatsAudioFeatures?> GetAudioFeaturesAsync(string trackId, CancellationToken cancellationToken)
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("ReccoBeats_GetAudioFeatures");
        activity?.SetTag("track.id", trackId);

        _logger.LogDebug("Fetching audio features for track {TrackId} from ReccoBeats", trackId);

        int maxRetries = 3;
        int retryCount = 0;

        while (retryCount <= maxRetries)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                HttpResponseMessage response = await _httpClient.GetAsync($"v1/track/{trackId}/audio-features", cancellationToken);

                // Handle rate limiting (429 Too Many Requests)
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    retryCount++;
                    
                    if (retryCount > maxRetries)
                    {
                        _logger.LogWarning("Max retries reached for track {TrackId} due to rate limiting", trackId);
                        activity?.SetTag("rate_limit.max_retries_reached", true);
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

                    _logger.LogInformation("Rate limited for track {TrackId}, waiting {RetryAfter} seconds before retry {RetryCount}/{MaxRetries}", 
                        trackId, retryAfter.TotalSeconds, retryCount, maxRetries);

                    activity?.SetTag("rate_limit.retry_after_seconds", retryAfter.TotalSeconds);
                    activity?.SetTag("rate_limit.retry_count", retryCount);

                    await Task.Delay(retryAfter, cancellationToken);
                    continue;
                }

                // Handle 404 Not Found (track not available)
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Track {TrackId} not found in ReccoBeats database", trackId);
                    activity?.SetTag("track.not_found", true);
                    return null;
                }

                response.EnsureSuccessStatusCode();

                ReccoBeatsAudioFeaturesResponse? result = await response.Content.ReadFromJsonAsync<ReccoBeatsAudioFeaturesResponse>(cancellationToken);

                if (result?.Success == true && result.Data != null)
                {
                    _logger.LogDebug("Successfully fetched audio features for track {TrackId}", trackId);
                    activity?.SetTag("success", true);
                    return result.Data;
                }

                _logger.LogWarning("ReccoBeats API returned unsuccessful response for track {TrackId}", trackId);
                return null;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "HTTP error fetching audio features for track {TrackId}", trackId);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                return null;
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Audio features request cancelled for track {TrackId}", trackId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error fetching audio features for track {TrackId}", trackId);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                return null;
            }
        }

        return null;
    }
}

