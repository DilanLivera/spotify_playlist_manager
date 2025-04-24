namespace UI.Services;

public sealed class SpotifyStateService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SpotifyStateService(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public void StoreTokens(string accessToken, string refreshToken)
    {
        _httpContextAccessor.HttpContext?.Session.SetString("SpotifyAccessToken", accessToken);
        _httpContextAccessor.HttpContext?.Session.SetString("SpotifyRefreshToken", refreshToken);
    }

    public string GetAccessToken() => _httpContextAccessor.HttpContext?.Session.GetString(key: "SpotifyAccessToken") ?? string.Empty;

    public string GetRefreshToken() => _httpContextAccessor.HttpContext?.Session.GetString(key: "SpotifyRefreshToken") ?? string.Empty;

    public bool IsAuthenticated() => !string.IsNullOrEmpty(GetAccessToken());
}