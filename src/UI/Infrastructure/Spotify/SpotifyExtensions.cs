namespace UI.Infrastructure.Spotify;

/// <summary>
/// Extension methods for configuring Spotify services and endpoints.
/// </summary>
public static class SpotifyExtensions
{
    /// <summary>
    /// Adds Spotify services to the application.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSpotifyServices(this IServiceCollection services)
    {
        // Register auth-related services
        services.AddScoped<SpotifyAuthService>();
        services.AddScoped<SpotifyAuthSessionManager>();

        // Register the authentication handler
        services.AddTransient<SpotifyAuthenticationHandler>();

        // Register SpotifyService with the authentication handler (kept for backward compatibility)
        services.AddHttpClient<SpotifyService>()
                .AddHttpMessageHandler<SpotifyAuthenticationHandler>();

        // Register specialized Spotify services with the authentication handler
        services.AddHttpClient<SpotifyPlaylistService>()
                .AddHttpMessageHandler<SpotifyAuthenticationHandler>();

        services.AddHttpClient<SpotifyTrackService>()
                .AddHttpMessageHandler<SpotifyAuthenticationHandler>();

        services.AddHttpClient<SpotifyUserService>()
                .AddHttpMessageHandler<SpotifyAuthenticationHandler>();

        // Register SpotifyTrackEnricher with the authentication handler
        services.AddHttpClient<SpotifyTrackEnricher>()
                .AddHttpMessageHandler<SpotifyAuthenticationHandler>()
                .ConfigureHttpClient(client =>
                {
                    client.BaseAddress = new Uri("https://api.spotify.com/v1/");
                });

        return services;
    }

    /// <summary>
    /// Configures Spotify endpoints in the application pipeline.
    /// </summary>
    /// <remarks>
    /// These endpoints handle Spotify authentication flow:
    /// - /spotify-auth: Initiates the OAuth2 authorization flow by redirecting to Spotify's authorization page
    /// - /callback: Processes the authorization code from Spotify, exchanges it for access/refresh tokens,
    ///   and stores them for subsequent API calls to Spotify services
    /// </remarks>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication UseSpotifyEndpoints(this WebApplication app)
    {
        app.MapGet(pattern: "/spotify-auth",
                   (SpotifyAuthService spotifyAuthService) =>
                   {
                       string authUrl = spotifyAuthService.GetAuthorizationUrl();

                       return Results.Redirect(authUrl);
                   });

        app.MapGet(pattern: "/callback",
                   async (
                       HttpContext context,
                       SpotifyAuthService spotifyAuthService,
                       SpotifyAuthSessionManager spotifyAuthSessionManager) =>
                   {
                       string? code = context.Request.Query["code"];

                       if (string.IsNullOrEmpty(code))
                       {
                           return Results.BadRequest(error: "Authorization code is missing");
                       }

                       (string? accessToken, string? refreshToken) = await spotifyAuthService.ExchangeCodeForTokenAsync(code);

                       if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
                       {
                           return Results.BadRequest(error: "Failed to get access token");
                       }

                       spotifyAuthSessionManager.StoreTokens(accessToken, refreshToken);

                       return Results.Redirect(url: "/");
                   });

        return app;
    }
}