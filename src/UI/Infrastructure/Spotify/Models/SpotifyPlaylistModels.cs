using System.Text.Json.Serialization;

namespace UI.Infrastructure.Spotify.Models;

/// <summary>
/// Represents a playlist from Spotify API.
/// </summary>
public sealed record SpotifyPlaylist
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("images")]
    public List<SpotifyImage> Images { get; init; } = [];

    [JsonPropertyName("tracks")]
    public PlaylistTracks Tracks { get; init; } = new();

    public string GetPlaylistImageUrl() => Images.FirstOrDefault()?.Url ?? string.Empty;
}

/// <summary>
/// Represents track metadata within a playlist.
/// </summary>
public sealed record PlaylistTracks
{
    [JsonPropertyName("href")]
    public string Href { get; init; } = string.Empty;

    [JsonPropertyName("total")]
    public int Total { get; init; }
}

/// <summary>
/// Response model for user playlists endpoint.
/// </summary>
public sealed record UserPlaylistsResponse
{
    [JsonPropertyName("items")]
    public List<SpotifyPlaylist> Items { get; init; } = [];
}

/// <summary>
/// Response model for playlist tracks endpoint.
/// </summary>
public sealed record PlaylistTrackResponse
{
    [JsonPropertyName("items")]
    public List<PlaylistTrackItem> Items { get; init; } = [];
}

/// <summary>
/// Represents a track item within a playlist response.
/// </summary>
public sealed record PlaylistTrackItem
{
    [JsonPropertyName("track")]
    public SpotifyTrack Track { get; init; } = new();
}

/// <summary>
/// Request model for creating a playlist.
/// </summary>
public sealed record CreatePlaylistRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("public")]
    public bool Public { get; init; } = false;
}

/// <summary>
/// Request model for adding tracks to a playlist.
/// </summary>
public sealed record AddTracksRequest
{
    [JsonPropertyName("uris")]
    public required IEnumerable<string> Uris { get; init; }
}

