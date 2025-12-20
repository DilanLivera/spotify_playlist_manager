using UI.Infrastructure.Spotify;

namespace UI.Features.PlaylistDetails;

/// <summary>
/// Interface for filtering tracks based on configurable criteria.
/// </summary>
public interface ITrackFilter
{
    /// <summary>
    /// Display name for this filter.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Suggested playlist name when copying filtered tracks.
    /// </summary>
    string SuggestedPlaylistName { get; }

    /// <summary>
    /// Determines if a track matches the filter criteria.
    /// </summary>
    bool Matches(SpotifyTrack track);
}

/// <summary>
/// Filters tracks by release year range.
/// </summary>
public sealed class YearRangeFilter(int minYear, int? maxYear = null) : ITrackFilter
{
    public string Name => maxYear.HasValue
        ? $"Songs {minYear}-{maxYear}"
        : $"Songs from {minYear} onwards";

    public string SuggestedPlaylistName => maxYear.HasValue
        ? $"Classics {minYear}-{maxYear}"
        : $"Modern Classics ({minYear}+)";

    public bool Matches(SpotifyTrack track)
    {
        int? year = ParseReleaseYear(track.Album.ReleaseDate);

        if (!year.HasValue)
        {
            return false;
        }

        if (year.Value < minYear)
        {
            return false;
        }

        if (maxYear.HasValue && year.Value > maxYear.Value)
        {
            return false;
        }

        return true;
    }

    private static int? ParseReleaseYear(string releaseDate)
    {
        if (string.IsNullOrEmpty(releaseDate) || releaseDate.Length < 4)
        {
            return null;
        }

        if (int.TryParse(releaseDate[..4], out int year))
        {
            return year;
        }

        return null;
    }
}

/// <summary>
/// Combines multiple filters with AND logic - track must match all filters.
/// </summary>
public sealed class CompositeFilter : ITrackFilter
{
    private readonly List<ITrackFilter> _filters;

    public CompositeFilter(IEnumerable<ITrackFilter> filters)
    {
        _filters = filters.ToList();

        if (_filters.Count == 0)
        {
            throw new ArgumentException("At least one filter is required", nameof(filters));
        }
    }

    public string Name => string.Join(" + ", _filters.Select(f => f.Name));

    public string SuggestedPlaylistName => _filters.First().SuggestedPlaylistName;

    public bool Matches(SpotifyTrack track) => _filters.All(f => f.Matches(track));
}

