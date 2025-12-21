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

    /// <summary>
    /// A confidence measure from 0.0 to 1.0 of whether the track is acoustic.
    /// 1.0 represents high confidence the track is acoustic.
    /// </summary>
    public float Acousticness { get; }

    /// <summary>
    /// Describes how suitable a track is for dancing based on tempo, rhythm stability,
    /// beat strength, and overall regularity. 0.0 is least danceable and 1.0 is most danceable.
    /// </summary>
    public float Danceability { get; }

    /// <summary>
    /// Represents a perceptual measure of intensity and activity from 0.0 to 1.0.
    /// Energetic tracks feel fast, loud, and noisy.
    /// </summary>
    public float Energy { get; }

    /// <summary>
    /// Predicts whether a track contains no vocals from 0.0 to 1.0.
    /// Values above 0.5 represent instrumental tracks.
    /// </summary>
    public float Instrumentalness { get; }

    /// <summary>
    /// The key the track is in. Integers map to pitches using standard Pitch Class notation.
    /// E.g. 0 = C, 1 = C♯/D♭, 2 = D, and so on. If no key was detected, the value is -1.
    /// </summary>
    public int Key { get; }

    /// <summary>
    /// Detects the presence of an audience in the recording from 0.0 to 1.0.
    /// Higher values represent increased probability the track was performed live.
    /// </summary>
    public float Liveness { get; }

    /// <summary>
    /// The overall loudness of a track in decibels (dB).
    /// Values typically range between -60 and 0 dB.
    /// </summary>
    public float Loudness { get; }

    /// <summary>
    /// Indicates the modality (major or minor) of a track.
    /// Major is represented by 1 and minor is 0.
    /// </summary>
    public int Mode { get; }

    /// <summary>
    /// Detects the presence of spoken words in a track from 0.0 to 1.0.
    /// Values above 0.66 describe tracks that are probably made entirely of spoken words.
    /// </summary>
    public float Speechiness { get; }

    /// <summary>
    /// The overall estimated tempo of a track in beats per minute (BPM).
    /// </summary>
    public float Tempo { get; }

    /// <summary>
    /// A measure from 0.0 to 1.0 describing the musical positiveness conveyed by a track.
    /// Tracks with high valence sound more positive (happy, cheerful, euphoric), while
    /// tracks with low valence sound more negative (sad, depressed, angry).
    /// </summary>
    public float Valence { get; }

    public Track(
        string id,
        string name,
        IReadOnlyList<Artist> artists,
        Album album,
        string genre,
        float acousticness = 0,
        float danceability = 0,
        float energy = 0,
        float instrumentalness = 0,
        int key = 0,
        float liveness = 0,
        float loudness = 0,
        int mode = 0,
        float speechiness = 0,
        float tempo = 0,
        float valence = 0)
    {
        Id = id;
        Name = name;
        Artists = artists;
        Album = album;
        Genre = string.IsNullOrEmpty(genre) ? "unknown" : genre;
        Acousticness = acousticness;
        Danceability = danceability;
        Energy = energy;
        Instrumentalness = instrumentalness;
        Key = key;
        Liveness = liveness;
        Loudness = loudness;
        Mode = mode;
        Speechiness = speechiness;
        Tempo = tempo;
        Valence = valence;
    }

    /// <summary>
    /// Gets the mood label based on audio features (Valence and Energy).
    /// Returns: Upbeat/Happy, Chill/Calm, Sad/Gloomy, Angry/Aggressive, or Neutral.
    /// </summary>
    public string GetMood()
    {
        if (Valence > 0.6f && Energy > 0.6f)
            return "Upbeat/Happy";
        if (Valence > 0.5f && Energy < 0.4f)
            return "Chill/Calm";
        if (Valence < 0.3f && Energy < 0.3f)
            return "Sad/Gloomy";
        if (Valence < 0.3f && Energy > 0.7f)
            return "Angry/Aggressive";
        return "Neutral";
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

