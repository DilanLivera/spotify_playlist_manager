namespace UI.Infrastructure.Spotify;

public sealed class SpotifyAuthSessionManager
{
    private const string SpotifyAccessToken = "SpotifyAccessToken";
    private const string SpotifyRefreshToken = "SpotifyRefreshToken";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SpotifyAuthSessionManager(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public void StoreTokens(string accessToken, string refreshToken)
    {
        _httpContextAccessor.HttpContext?.Session.SetString(SpotifyAccessToken, accessToken);
        _httpContextAccessor.HttpContext?.Session.SetString(SpotifyRefreshToken, refreshToken);
    }

    public string GetAccessToken() => _httpContextAccessor.HttpContext?.Session.GetString(key: SpotifyAccessToken) ?? string.Empty;

    public string GetRefreshToken() => _httpContextAccessor.HttpContext?.Session.GetString(key: SpotifyRefreshToken) ?? string.Empty;

    public void UpdateAccessToken(string newAccessToken) => _httpContextAccessor.HttpContext?.Session.SetString(SpotifyAccessToken, newAccessToken);

    public bool IsAuthenticated() => !string.IsNullOrEmpty(GetAccessToken());
}