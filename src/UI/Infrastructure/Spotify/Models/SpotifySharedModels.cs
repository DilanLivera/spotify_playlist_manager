using System.Text.Json.Serialization;

namespace UI.Infrastructure.Spotify.Models;

/// <summary>
/// Represents an image from Spotify API.
/// </summary>
public sealed record SpotifyImage
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("height")]
    public int? Height { get; init; }

    [JsonPropertyName("width")]
    public int? Width { get; init; }
}

/// <summary>
/// Represents an artist from Spotify API.
/// </summary>
public sealed record SpotifyArtist
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("genres")]
    public List<string> Genres { get; init; } = [];
}

