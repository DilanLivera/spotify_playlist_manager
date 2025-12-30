using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;

namespace UI.Infrastructure.Spotify;

/// <summary>
/// HTTP message handler that automatically manages Spotify API authentication.
/// Injects Bearer tokens and handles token refresh on 401 responses.
/// </summary>
public sealed class SpotifyAuthenticationHandler : DelegatingHandler
{
    private readonly SpotifyAuthSessionManager _sessionManager;
    private readonly SpotifyAuthService _authService;
    private readonly ILogger<SpotifyAuthenticationHandler> _logger;

    public SpotifyAuthenticationHandler(
        SpotifyAuthSessionManager sessionManager,
        SpotifyAuthService authService,
        ILogger<SpotifyAuthenticationHandler> logger)
    {
        _sessionManager = sessionManager;
        _authService = authService;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string accessToken = _sessionManager.GetAccessToken();
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(scheme: "Bearer", accessToken);
        }

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        _logger.LogWarning("Received 401 Unauthorized, attempting token refresh");

        if (await _authService.RefreshTokenAsync())
        {
            _logger.LogDebug("Token refreshed successfully, retrying request");

            string newAccessToken = _sessionManager.GetAccessToken();
            request.Headers.Authorization = new AuthenticationHeaderValue(scheme: "Bearer", newAccessToken);

            return await base.SendAsync(request, cancellationToken);
        }

        _logger.LogError("Token refresh failed, user needs to re-authenticate");

        Activity.Current?.SetStatus(ActivityStatusCode.Error, description: "Token refresh failed");

        return response;
    }
}