using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using UI.Infrastructure.Observability;

namespace UI.Infrastructure.Spotify;

public sealed class SpotifyAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SpotifyAuthService> _logger;
    private readonly SpotifyAuthSessionManager _sessionManager;

    public SpotifyAuthService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SpotifyAuthService> logger,
        SpotifyAuthSessionManager sessionManager)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _sessionManager = sessionManager;
    }

    public string GetAuthorizationUrl()
    {
        _logger.LogDebug("Building Spotify authorization URL");

        string? clientId = _configuration["Spotify:ClientId"];
        string? redirectUri = _configuration["Spotify:RedirectUri"];

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
        {
            _logger.LogError("Spotify configuration missing: ClientId={HasClientId}, RedirectUri={HasRedirectUri}",
                !string.IsNullOrEmpty(clientId), !string.IsNullOrEmpty(redirectUri));
            return string.Empty;
        }

        Dictionary<string, string?> queryParams = new()
                                                  {
                                                      { "client_id", clientId },
                                                      { "response_type", "code" },
                                                      { "redirect_uri", redirectUri },
                                                      // https://developer.spotify.com/documentation/web-api/concepts/scopes
                                                      // please use %20 instead of " " when adding scopes.
                                                      { "scope", "playlist-read-private playlist-modify-private playlist-modify-public user-read-recently-played" }
                                                  };

        _logger.LogDebug("Authorization URL built with redirect URI {RedirectUri}", redirectUri);

        return QueryHelpers.AddQueryString(uri: "https://accounts.spotify.com/authorize", queryParams);
    }

    public async Task<(string AccessToken, string RefreshToken)> ExchangeCodeForTokenAsync(string code)
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("ExchangeCodeForToken");

        _logger.LogDebug("Exchanging authorization code for tokens");

        try
        {
            string? clientId = _configuration["Spotify:ClientId"];
            string? clientSecret = _configuration["Spotify:ClientSecret"];
            string? redirectUri = _configuration["Spotify:RedirectUri"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(redirectUri))
            {
                _logger.LogError("Spotify configuration incomplete: ClientId={HasClientId}, ClientSecret={HasClientSecret}, RedirectUri={HasRedirectUri}",
                    !string.IsNullOrEmpty(clientId), !string.IsNullOrEmpty(clientSecret), !string.IsNullOrEmpty(redirectUri));
                activity?.SetStatus(ActivityStatusCode.Error, "Missing configuration");
                return (AccessToken: string.Empty, RefreshToken: string.Empty);
            }

            string auth = Convert.ToBase64String(inArray: Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "Basic", auth);

            Dictionary<string, string> parameters = new() { { "grant_type", "authorization_code" }, { "code", code }, { "redirect_uri", redirectUri } };
            FormUrlEncodedContent content = new(parameters);

            HttpResponseMessage response = await _httpClient.PostAsync(requestUri: "https://accounts.spotify.com/api/token", content);

            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync();
            using JsonDocument document = JsonDocument.Parse(responseBody);

            string accessToken = document.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
            string refreshToken = document.RootElement.GetProperty("refresh_token").GetString() ?? string.Empty;

            _logger.LogInformation("Successfully exchanged authorization code for tokens");
            activity?.SetTag("auth.success", true);

            return (accessToken, refreshToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error exchanging code for tokens: {StatusCode}", ex.StatusCode);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return (AccessToken: string.Empty, RefreshToken: string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error exchanging code for tokens");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return (AccessToken: string.Empty, RefreshToken: string.Empty);
        }
    }

    public async Task<string> RefreshAccessTokenAsync(string refreshToken)
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("RefreshAccessToken");

        _logger.LogDebug("Refreshing access token");

        try
        {
            string? clientId = _configuration["Spotify:ClientId"];
            string? clientSecret = _configuration["Spotify:ClientSecret"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                _logger.LogError("Spotify client credentials not configured");
                activity?.SetStatus(ActivityStatusCode.Error, "Missing credentials");
                return string.Empty;
            }

            string auth = Convert.ToBase64String(inArray: Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "Basic", auth);

            Dictionary<string, string> parameters = new() { { "grant_type", "refresh_token" }, { "refresh_token", refreshToken } };
            FormUrlEncodedContent content = new(parameters);

            HttpResponseMessage response = await _httpClient.PostAsync(requestUri: "https://accounts.spotify.com/api/token", content);
            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync();
            using JsonDocument document = JsonDocument.Parse(responseBody);

            _logger.LogDebug("Access token refreshed successfully");
            activity?.SetTag("auth.refresh_success", true);

            return document.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error refreshing access token: {StatusCode}", ex.StatusCode);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error refreshing access token");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return string.Empty;
        }
    }

    /// <summary>
    /// Refreshes the access token using the stored refresh token and updates the session.
    /// </summary>
    /// <returns>True if token refresh was successful, false otherwise.</returns>
    public async Task<bool> RefreshTokenAsync()
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("RefreshToken");

        string refreshToken = _sessionManager.GetRefreshToken();
        if (string.IsNullOrEmpty(refreshToken))
        {
            _logger.LogWarning("No refresh token available in session, user needs to re-authenticate");
            activity?.SetStatus(ActivityStatusCode.Error, "No refresh token");
            return false;
        }

        string newAccessToken = await RefreshAccessTokenAsync(refreshToken);
        if (string.IsNullOrEmpty(newAccessToken))
        {
            _logger.LogError("Token refresh failed, user needs to re-authenticate");
            activity?.SetStatus(ActivityStatusCode.Error, "Refresh failed");
            return false;
        }

        _sessionManager.UpdateAccessToken(newAccessToken);
        _logger.LogInformation("Session updated with new access token");
        activity?.SetTag("auth.session_updated", true);
        return true;
    }
}