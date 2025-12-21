using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using UI.Infrastructure.Observability;

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
        using Activity? activity = ObservabilityExtensions.StartActivity("SpotifyAuthHandler");

        // Inject access token into request
        string accessToken = _sessionManager.GetAccessToken();
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        // Execute the request
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        // Handle 401 Unauthorized by attempting token refresh
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("Received 401 Unauthorized, attempting token refresh");
            activity?.SetTag("auth.refresh_triggered", true);

            if (await _authService.RefreshTokenAsync())
            {
                _logger.LogDebug("Token refreshed successfully, retrying request");

                // Update the request with the new token
                string newAccessToken = _sessionManager.GetAccessToken();
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newAccessToken);

                // Retry the original request
                response = await base.SendAsync(request, cancellationToken);
                activity?.SetTag("auth.retry_success", response.IsSuccessStatusCode);
            }
            else
            {
                _logger.LogError("Token refresh failed, user needs to re-authenticate");
                activity?.SetStatus(ActivityStatusCode.Error, "Token refresh failed");
            }
        }

        return response;
    }
}