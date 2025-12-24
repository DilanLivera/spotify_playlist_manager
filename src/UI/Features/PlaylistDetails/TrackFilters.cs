using UI.Features.Shared.Domain;
using UI.Infrastructure.AIAgent;

namespace UI.Features.PlaylistDetails;

/// <summary>
/// Interface for filtering tracks based on configurable criteria.
/// </summary>
public interface ITrackFilter
{
    /// <summary>
    /// Display name for this filter.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Suggested playlist name when copying filtered tracks.
    /// </summary>
    public string SuggestedPlaylistName { get; }

    /// <summary>
    /// Determines if a track matches the filter criteria.
    /// </summary>
    public bool Matches(Track track);
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

    public bool Matches(Track track)
    {
        int? year = track.GetReleaseYear();

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

    public bool Matches(Track track) => _filters.All(f => f.Matches(track));
}

/// <summary>
/// Filters tracks using AI-powered natural language analysis.
/// Evaluates tracks against user-provided criteria via Ollama LLM.
/// </summary>
public sealed class AiTrackFilter : ITrackFilter
{
    private readonly string _userPrompt;
    private readonly string _suggestedPlaylistName;
    private readonly HashSet<string> _matchingTrackIds;

    /// <summary>
    /// Creates an AI-powered track filter that has already been evaluated.
    /// </summary>
    /// <param name="userPrompt">The natural language filtering criteria</param>
    /// <param name="suggestedPlaylistName">AI-generated suggested playlist name</param>
    /// <param name="matchingTrackIds">Set of track IDs that match the criteria</param>
    public AiTrackFilter(string userPrompt, string suggestedPlaylistName, HashSet<string> matchingTrackIds)
    {
        _userPrompt = userPrompt;
        _suggestedPlaylistName = suggestedPlaylistName;
        _matchingTrackIds = matchingTrackIds;
    }

    public string Name => $"AI: {_userPrompt}";

    public string SuggestedPlaylistName => _suggestedPlaylistName;

    /// <summary>
    /// Checks if a track matches the AI filter by looking it up in the pre-computed set.
    /// </summary>
    public bool Matches(Track track) => _matchingTrackIds.Contains(track.Id);

    /// <summary>
    /// Factory method to create an AITrackFilter by running AI analysis on tracks.
    /// </summary>
    /// <param name="userPrompt">Natural language filtering criteria</param>
    /// <param name="tracks">Tracks to filter</param>
    /// <param name="aiService">AI track filter service</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A new AITrackFilter with pre-computed matching track IDs</returns>
    public static async Task<AiTrackFilter> CreateAsync(
        string userPrompt,
        IEnumerable<Track> tracks,
        AiTrackFilterService aiService,
        CancellationToken cancellationToken = default)
    {
        // Run AI filtering to get matching track IDs
        HashSet<string> matchingTrackIds = await aiService.FilterTracksAsync(userPrompt, tracks, cancellationToken);

        // Generate a suggested playlist name
        string suggestedPlaylistName = await aiService.GeneratePlaylistNameAsync(userPrompt, cancellationToken);

        return new AiTrackFilter(userPrompt, suggestedPlaylistName, matchingTrackIds);
    }
}