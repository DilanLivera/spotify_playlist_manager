using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

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
        string? clientId = _configuration["Spotify:ClientId"];
        string? redirectUri = _configuration["Spotify:RedirectUri"];

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
        {
            _logger.LogError("Spotify client ID or redirect URI is not configured.");
            return string.Empty;
        }

        Dictionary<string, string?> queryParams = new()
                                                  {
                                                      { "client_id", clientId },
                                                      { "response_type", "code" },
                                                      { "redirect_uri", redirectUri },
                                                      // https://developer.spotify.com/documentation/web-api/concepts/scopes
                                                      // please use %20 instead of " " when adding scopes.
                                                      { "scope", "playlist-read-private user-read-recently-played" }
                                                  };

        return QueryHelpers.AddQueryString(uri: "https://accounts.spotify.com/authorize", queryParams);
    }

    public async Task<(string AccessToken, string RefreshToken)> ExchangeCodeForTokenAsync(string code)
    {
        try
        {
            string? clientId = _configuration["Spotify:ClientId"];
            string? clientSecret = _configuration["Spotify:ClientSecret"];
            string? redirectUri = _configuration["Spotify:RedirectUri"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(redirectUri))
            {
                _logger.LogError("Spotify client credentials are not properly configured.");
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

            return (accessToken, refreshToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging code for tokens");
            return (AccessToken: string.Empty, RefreshToken: string.Empty);
        }
    }

    public async Task<string> RefreshAccessTokenAsync(string refreshToken)
    {
        try
        {
            string? clientId = _configuration["Spotify:ClientId"];
            string? clientSecret = _configuration["Spotify:ClientSecret"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                _logger.LogError("Spotify client credentials are not properly configured.");
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

            return document.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing access token");
            return string.Empty;
        }
    }

    /// <summary>
    /// Refreshes the access token using the stored refresh token and updates the session.
    /// </summary>
    /// <returns>True if token refresh was successful, false otherwise.</returns>
    public async Task<bool> RefreshTokenAsync()
    {
        string refreshToken = _sessionManager.GetRefreshToken();
        if (string.IsNullOrEmpty(refreshToken))
        {
            _logger.LogWarning("No refresh token available for token refresh");
            return false;
        }

        string newAccessToken = await RefreshAccessTokenAsync(refreshToken);
        if (string.IsNullOrEmpty(newAccessToken))
        {
            _logger.LogError("Failed to refresh access token");
            return false;
        }

        _sessionManager.UpdateAccessToken(newAccessToken);
        _logger.LogInformation("Successfully refreshed access token");
        return true;
    }
}