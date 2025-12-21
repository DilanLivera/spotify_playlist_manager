namespace UI.Features.Shared.Domain;

/// <summary>
/// Domain aggregate root representing a Spotify playlist.
/// Encapsulates playlist data and business logic for track organization.
/// </summary>
public sealed class Playlist
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string ImageUrl { get; }
    public int TrackCount { get; }

    public Playlist(string id, string name, string description, string imageUrl, int trackCount)
    {
        Id = id;
        Name = name;
        Description = description;
        ImageUrl = imageUrl;
        TrackCount = trackCount;
    }

    /// <summary>
    /// Groups tracks by their genre.
    /// </summary>
    public Dictionary<string, List<Track>> GroupTracksByGenre(IEnumerable<Track> tracks) => tracks.GroupBy(t => t.Genre)
                                                                                                  .ToDictionary(g => g.Key, g => g.ToList());

    /// <summary>
    /// Groups tracks by their decade.
    /// </summary>
    public Dictionary<string, List<Track>> GroupTracksByDecade(IEnumerable<Track> tracks) => tracks.GroupBy(t => t.GetDecade())
                                                                                                   .OrderBy(g => g.Key)
                                                                                                   .ToDictionary(g => g.Key, g => g.ToList());

    /// <summary>
    /// Filters tracks using the provided filter.
    /// </summary>
    public IEnumerable<Track> FilterTracks(IEnumerable<Track> tracks, Func<Track, bool> predicate) => tracks.Where(predicate);
}