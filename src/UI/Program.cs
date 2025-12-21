using UI.App;
using UI.Infrastructure.Auth;
using UI.Infrastructure.Observability;
using UI.Infrastructure.ReccoBeats;
using UI.Infrastructure.Spotify;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddObservability(builder.Configuration);
builder.Logging.AddObservabilityLogging(builder.Configuration);

builder.Services
       .AddRazorComponents()
       .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();

builder.Services.AddHttpClient();

builder.Services.AddSpotifyServices();

builder.Services.AddHttpClient<ReccoBeatsService>();

builder.Services.AddApplicationAuth(builder.Configuration);

builder.Services.AddAuthorizationCore();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(1);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

WebApplication app = builder.Build();

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

app.UseApplicationAuth();

app.UseSpotifyEndpoints();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Run();