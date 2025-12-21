using System.Text.Json.Serialization;

namespace UI.Infrastructure.Spotify.Models;

/// <summary>
/// Response model for artists endpoint.
/// </summary>
public sealed record ArtistsResponse
{
    [JsonPropertyName("artists")]
    public List<SpotifyArtist> Artists { get; init; } = [];
}

