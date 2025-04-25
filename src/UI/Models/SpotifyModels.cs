using System.Text.Json.Serialization;

namespace UI.Models;

public sealed class SpotifyPlaylist
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

public sealed class PlaylistTracks
{
    [JsonPropertyName("href")]
    public string Href { get; init; } = string.Empty;

    [JsonPropertyName("total")]
    public int Total { get; init; }
}

public sealed class SpotifyImage
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("height")]
    public int? Height { get; init; }

    [JsonPropertyName("width")]
    public int? Width { get; init; }
}

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

    public string GetArtistsString() => string.Join(", ", Artists.Select(a => a.Name));
}

public sealed class SpotifyArtist
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("genres")]
    public List<string> Genres { get; init; } = [];
}

public sealed class SpotifyAlbum
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

public sealed class PlaylistTrackResponse
{
    [JsonPropertyName("items")]
    public List<PlaylistTrackItem> Items { get; init; } = [];
}

public sealed class PlaylistTrackItem
{
    [JsonPropertyName("track")]
    public SpotifyTrack Track { get; init; } = new();
}

public sealed class UserPlaylistsResponse
{
    [JsonPropertyName("items")]
    public List<SpotifyPlaylist> Items { get; init; } = [];
}