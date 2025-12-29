using System.Diagnostics;
using UI.Infrastructure.Observability;
using UI.Infrastructure.Spotify.Models;

namespace UI.Infrastructure.Spotify;

/// <summary>
/// Service for fetching Spotify user information.
/// </summary>
public sealed class SpotifyUserService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SpotifyUserService> _logger;

    public SpotifyUserService(
        HttpClient httpClient,
        ILogger<SpotifyUserService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _httpClient.BaseAddress = new Uri("https://api.spotify.com/v1/");
    }

    public async Task<SpotifyUser> GetCurrentUserAsync()
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("GetCurrentUser");

        _logger.LogDebug("Fetching current user from Spotify API");

        try
        {
            SpotifyUser user = await _httpClient.GetFromJsonAsync<SpotifyUser>(requestUri: "me") ?? throw new InvalidOperationException("Response can not be null");

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
}