using UI.Features.Shared.Domain;
using UI.Infrastructure.ReccoBeats;

namespace UI.Infrastructure.Spotify;

/// <summary>
/// Extension methods for mapping Spotify API DTOs to Domain entities.
/// </summary>
public static class SpotifyMappingExtensions
{
    /// <summary>
    /// Maps a Spotify API track DTO to a Domain Track entity.
    /// </summary>
    public static Track MapToDomain(this SpotifyTrack dto, Dictionary<string, ReccoBeatsAudioFeatures>? audioFeatures = null)
    {
        IReadOnlyList<Artist> artists = dto.Artists
            .Select(a => new Artist(id: a.Id, name: a.Name))
            .ToList()
            .AsReadOnly();

        Album album = new(id: dto.Album.Id,
                          name: dto.Album.Name,
                          imageUrl: dto.Album.GetAlbumImageUrl(),
                          releaseDate: dto.Album.ReleaseDate);

        ReccoBeatsAudioFeatures? features = null;
        audioFeatures?.TryGetValue(dto.Id, out features);

        return new Track(
            id: dto.Id,
            name: dto.Name,
            artists: artists,
            album: album,
            genre: dto.Genre,
            acousticness: features?.Acousticness ?? 0,
            danceability: features?.Danceability ?? 0,
            energy: features?.Energy ?? 0,
            instrumentalness: features?.Instrumentalness ?? 0,
            key: features?.Key ?? 0,
            liveness: features?.Liveness ?? 0,
            loudness: features?.Loudness ?? 0,
            mode: features?.Mode ?? 0,
            speechiness: features?.Speechiness ?? 0,
            tempo: features?.Tempo ?? 0,
            valence: features?.Valence ?? 0);
    }

    /// <summary>
    /// Maps a collection of Spotify API track DTOs to Domain Track entities.
    /// </summary>
    public static IReadOnlyList<Track> MapToDomain(this IEnumerable<SpotifyTrack> dtos,
                                                   Dictionary<string, ReccoBeatsAudioFeatures>? audioFeatures = null)
        => dtos.Select(t => t.MapToDomain(audioFeatures))
               .ToList()
               .AsReadOnly();

    /// <summary>
    /// Maps a Spotify API playlist DTO to a Domain Playlist entity.
    /// </summary>
    public static Playlist MapToDomain(this SpotifyPlaylist dto) => new(id: dto.Id,
                                                                        name: dto.Name,
                                                                        description: dto.Description,
                                                                        imageUrl: dto.GetPlaylistImageUrl(),
                                                                        trackCount: dto.Tracks.Total);

    /// <summary>
    /// Maps a collection of Spotify API playlist DTOs to Domain Playlist entities.
    /// </summary>
    public static IReadOnlyList<Playlist> MapToDomain(this IEnumerable<SpotifyPlaylist> dtos) => dtos.Select(MapToDomain)
                                                                                                     .ToList()
                                                                                                     .AsReadOnly();

}

