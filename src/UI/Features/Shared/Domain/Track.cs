namespace UI.Features.Shared.Domain;

/// <summary>
/// Domain entity representing a music track.
/// Encapsulates track data and business logic for formatting and calculations.
/// </summary>
public sealed class Track
{
    public string Id { get; }
    public string Name { get; }
    public IReadOnlyList<Artist> Artists { get; }
    public Album Album { get; }
    public string Genre { get; }

    public Track(string id, string name, IReadOnlyList<Artist> artists, Album album, string genre)
    {
        Id = id;
        Name = name;
        Artists = artists;
        Album = album;
        Genre = string.IsNullOrEmpty(genre) ? "unknown" : genre;
    }

    /// <summary>
    /// Gets the decade from the album release date (e.g., "1990s", "2000s").
    /// </summary>
    public string GetDecade()
    {
        if (string.IsNullOrEmpty(Album.ReleaseDate) || Album.ReleaseDate.Length < 4)
        {
            return "unknown";
        }

        if (int.TryParse(Album.ReleaseDate[..4], out int year))
        {
            int decade = year / 10 * 10;
            return $"{decade}s";
        }

        return "unknown";
    }

    /// <summary>
    /// Gets the release year from the album release date.
    /// </summary>
    public int? GetReleaseYear()
    {
        if (string.IsNullOrEmpty(Album.ReleaseDate) || Album.ReleaseDate.Length < 4)
        {
            return null;
        }

        if (int.TryParse(Album.ReleaseDate[..4], out int year))
        {
            return year;
        }

        return null;
    }

    /// <summary>
    /// Gets a comma-separated string of artist names.
    /// </summary>
    public string GetArtistDisplay() => string.Join(", ", Artists.Select(a => a.Name));

    /// <summary>
    /// Converts the track ID to a Spotify URI format.
    /// </summary>
    public string ToSpotifyUri() => $"spotify:track:{Id}";
}

/// <summary>
/// Domain entity representing an artist.
/// </summary>
public sealed class Artist
{
    public string Id { get; }
    public string Name { get; }

    public Artist(string id, string name)
    {
        Id = id;
        Name = name;
    }
}

/// <summary>
/// Domain entity representing an album.
/// </summary>
public sealed class Album
{
    public string Id { get; }
    public string Name { get; }
    public string ImageUrl { get; }
    public string ReleaseDate { get; }

    public Album(string id, string name, string imageUrl, string releaseDate)
    {
        Id = id;
        Name = name;
        ImageUrl = imageUrl;
        ReleaseDate = releaseDate;
    }
}

