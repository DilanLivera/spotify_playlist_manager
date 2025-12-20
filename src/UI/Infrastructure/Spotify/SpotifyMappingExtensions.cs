using UI.Features.Shared.Domain;

namespace UI.Infrastructure.Spotify;

/// <summary>
/// Extension methods for mapping Spotify API DTOs to Domain entities.
/// </summary>
public static class SpotifyMappingExtensions
{
    /// <summary>
    /// Maps a Spotify API track DTO to a Domain Track entity.
    /// </summary>
    public static Track MapToDomain(this SpotifyTrack dto)
    {
        IReadOnlyList<Artist> artists = dto.Artists
            .Select(a => new Artist(id: a.Id, name: a.Name))
            .ToList();

        Album album = new Album(
            id: dto.Album.Id,
            name: dto.Album.Name,
            imageUrl: dto.Album.GetAlbumImageUrl(),
            releaseDate: dto.Album.ReleaseDate);

        return new Track(
            id: dto.Id,
            name: dto.Name,
            artists: artists,
            album: album,
            genre: dto.Genre);
    }

    /// <summary>
    /// Maps a collection of Spotify API track DTOs to Domain Track entities.
    /// </summary>
    public static IReadOnlyList<Track> MapToDomain(this IEnumerable<SpotifyTrack> dtos)
    {
        return dtos.Select(MapToDomain).ToList();
    }

    /// <summary>
    /// Maps a Spotify API playlist DTO to a Domain Playlist entity.
    /// </summary>
    public static Playlist MapToDomain(this SpotifyPlaylist dto)
    {
        return new Playlist(
            id: dto.Id,
            name: dto.Name,
            description: dto.Description,
            imageUrl: dto.GetPlaylistImageUrl(),
            trackCount: dto.Tracks.Total);
    }

    /// <summary>
    /// Maps a collection of Spotify API playlist DTOs to Domain Playlist entities.
    /// </summary>
    public static IReadOnlyList<Playlist> MapToDomain(this IEnumerable<SpotifyPlaylist> dtos)
    {
        return dtos.Select(MapToDomain).ToList();
    }

    /// <summary>
    /// Maps a Domain Track entity back to a Spotify URI for API operations.
    /// </summary>
    public static string MapToSpotifyUri(this Track track)
    {
        return track.ToSpotifyUri();
    }

    /// <summary>
    /// Maps a collection of Domain Track entities to Spotify URIs.
    /// </summary>
    public static IEnumerable<string> MapToSpotifyUris(this IEnumerable<Track> tracks)
    {
        return tracks.Select(MapToSpotifyUri);
    }
}

