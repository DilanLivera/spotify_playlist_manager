using UI.Components;
using UI.Infrastructure;
using UI.Services;

WebApplicationBuilder? builder = WebApplication.CreateBuilder(args);

builder.Services
       .AddRazorComponents()
       .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();

builder.Services.AddHttpClient();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(1);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddScoped<SpotifyAuthService>();
builder.Services.AddScoped<SpotifyService>();
builder.Services.AddScoped<SpotifyStateService>();

builder.Services.AddApplicationAuth(builder.Configuration);

builder.Services.AddAuthorizationCore();

WebApplication? app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

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
               SpotifyStateService spotifyStateService,
               ILogger<Program> logger) =>
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

               spotifyStateService.StoreTokens(accessToken, refreshToken);

               return Results.Redirect(url: "/playlists");
           });

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Run();