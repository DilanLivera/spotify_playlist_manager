using System.Text.Json.Serialization;

namespace UI.Infrastructure.Spotify.Models;

/// <summary>
/// Represents a user from Spotify API.
/// </summary>
public sealed record SpotifyUser
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; init; } = string.Empty;
}

