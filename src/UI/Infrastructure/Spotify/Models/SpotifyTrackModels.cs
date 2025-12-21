using System.Text.Json.Serialization;

namespace UI.Infrastructure.Spotify.Models;

/// <summary>
/// Represents a track from Spotify API.
/// </summary>
public sealed class SpotifyTrack
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("artists")]
    public List<SpotifyArtist> Artists { get; init; } = [];

    [JsonPropertyName("album")]
    public SpotifyAlbum Album { get; init; } = new();

    public string Genre { get; set; } = string.Empty;

    public string GetArtistsAsString() => string.Join(", ", Artists.Select(a => a.Name));
}

/// <summary>
/// Represents an album from Spotify API.
/// </summary>
public sealed record SpotifyAlbum
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("images")]
    public List<SpotifyImage> Images { get; init; } = [];

    [JsonPropertyName("release_date")]
    public string ReleaseDate { get; init; } = string.Empty;

    public string GetAlbumImageUrl() => Images.FirstOrDefault()?.Url ?? string.Empty;
}

/// <summary>
/// Response model for audio features endpoint.
/// </summary>
public sealed record AudioFeaturesResponse
{
    [JsonPropertyName("audio_features")]
    public List<SpotifyAudioFeatures?> AudioFeatures { get; init; } = [];
}

/// <summary>
/// Represents audio features from Spotify API.
/// </summary>
public sealed record SpotifyAudioFeatures
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("valence")]
    public float Valence { get; init; }

    [JsonPropertyName("energy")]
    public float Energy { get; init; }

    [JsonPropertyName("danceability")]
    public float Danceability { get; init; }
}