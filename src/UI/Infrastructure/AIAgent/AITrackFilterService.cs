using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using UI.Features.Shared.Domain;
using UI.Infrastructure.Observability;

namespace UI.Infrastructure.AIAgent;

/// <summary>
/// Service for filtering tracks using AI-powered natural language analysis.
/// Uses Ollama (local LLM) to evaluate tracks against user criteria.
/// </summary>
public sealed class AITrackFilterService
{
    private readonly OllamaApiClient _ollamaClient;
    private readonly ILogger<AITrackFilterService> _logger;
    private readonly string _systemInstructions;

    public AITrackFilterService(
        OllamaApiClient ollamaClient,
        IConfiguration configuration,
        ILogger<AITrackFilterService> logger)
    {
        _ollamaClient = ollamaClient;
        _logger = logger;
        _systemInstructions = configuration["AIAgent:SystemInstructions"] ??
                              "You are a helpful music filtering assistant.";
    }

    /// <summary>
    /// Filters tracks based on natural language criteria using AI analysis.
    /// </summary>
    /// <param name="userPrompt">Natural language filtering criteria (e.g., "upbeat songs from the 2000s")</param>
    /// <param name="tracks">Collection of tracks to filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Set of track IDs that match the criteria</returns>
    public async Task<HashSet<string>> FilterTracksAsync(
        string userPrompt,
        IEnumerable<Track> tracks,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("AIFilterTracks");
        activity?.SetTag("ai.prompt", userPrompt);
        activity?.SetTag("ai.system_instructions", _systemInstructions);
        activity?.SetTag("ai.model", _ollamaClient.SelectedModel);

        List<Track> trackList = tracks.ToList();
        _logger.LogDebug("Starting AI filtering with prompt: {Prompt} for {TrackCount} tracks", userPrompt, trackList.Count);

        try
        {
            List<TrackFilterDto> trackDtos = trackList.Select(t => new TrackFilterDto
            {
                Id = t.Id,
                Name = t.Name,
                Artist = t.GetArtistDisplay(),
                Album = t.Album.Name,
                Year = t.GetReleaseYear(),
                Genre = t.Genre,
                Energy = t.Energy,
                Valence = t.Valence,
                Danceability = t.Danceability,
                Tempo = t.Tempo,
                Acousticness = t.Acousticness,
                Instrumentalness = t.Instrumentalness,
                Mood = t.GetMood()
            }).ToList();

            activity?.SetTag("ai.track_count", trackDtos.Count);

            string tracksJson = JsonSerializer.Serialize(trackDtos, new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            string fullPrompt = $"""
                User filtering request: "{userPrompt}"

                Analyze the following tracks and determine which ones match the user's criteria.
                Consider all available metadata including track name, artist, album, year, genre, and audio features.

                Audio features explanation:
                - Energy (0.0-1.0): Intensity and activity (higher = more energetic)
                - Valence (0.0-1.0): Musical positiveness (higher = happier/more positive)
                - Danceability (0.0-1.0): How suitable for dancing
                - Tempo: Beats per minute (BPM)
                - Acousticness (0.0-1.0): Confidence the track is acoustic
                - Instrumentalness (0.0-1.0): Likelihood track contains no vocals
                - Mood: Derived from energy and valence (Upbeat/Happy, Chill/Calm, Sad/Gloomy, Angry/Aggressive, Neutral)

                Tracks data:
                {tracksJson}

                Respond ONLY with a JSON array of track IDs that match the criteria, nothing else.
                Format: ["track_id_1", "track_id_2", ...]
                If no tracks match, return an empty array: []
                """;

            _logger.LogDebug("Sending prompt to Ollama (prompt length: {Length} chars)", fullPrompt.Length);

            activity?.SetTag("ai.full_prompt", fullPrompt);
            activity?.SetTag("ai.full_prompt_length", fullPrompt.Length);

            ChatRequest chatRequest = new()
            {
                Model = _ollamaClient.SelectedModel,
                Messages = new List<Message>
                {
                    new()
                    {
                        Role = "system",
                        Content = _systemInstructions
                    },
                    new()
                    {
                        Role = "user",
                        Content = fullPrompt
                    }
                }
            };

            string response = string.Empty;
            await foreach (ChatResponseStream? stream in _ollamaClient.Chat(chatRequest, cancellationToken))
            {
                if (stream?.Message?.Content != null)
                {
                    response += stream.Message.Content;
                }
            }

            _logger.LogDebug("Received Ollama response: {Response}", response);

            activity?.SetTag("ai.response", response);
            activity?.SetTag("ai.response_length", response.Length);

            HashSet<string> matchingTrackIds = ParseTrackIds(response);

            _logger.LogInformation("AI filtered {MatchCount} of {TotalCount} tracks for prompt: {Prompt}",
                                   matchingTrackIds.Count,
                                   trackList.Count,
                                   userPrompt);

            activity?.SetTag("ai.matched_count", matchingTrackIds.Count);

            return matchingTrackIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during AI track filtering with prompt: {Prompt}", userPrompt);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            throw;
        }
    }

    /// <summary>
    /// Parses the AI response to extract track IDs.
    /// Handles various response formats and cleans the JSON.
    /// </summary>
    private HashSet<string> ParseTrackIds(string response)
    {
        try
        {
            // Clean up the response - remove markdown code blocks if present
            string cleanedResponse = response.Trim();
            if (cleanedResponse.StartsWith("```"))
            {
                // Remove markdown code block markers
                int firstNewline = cleanedResponse.IndexOf('\n');
                int lastBackticks = cleanedResponse.LastIndexOf("```");
                if (firstNewline > 0 && lastBackticks > firstNewline)
                {
                    cleanedResponse = cleanedResponse.Substring(firstNewline + 1, lastBackticks - firstNewline - 1).Trim();
                }
            }

            // Try to find JSON array in the response
            int arrayStart = cleanedResponse.IndexOf('[');
            int arrayEnd = cleanedResponse.LastIndexOf(']');

            if (arrayStart >= 0 && arrayEnd > arrayStart)
            {
                cleanedResponse = cleanedResponse.Substring(arrayStart, arrayEnd - arrayStart + 1);
            }

            List<string>? trackIds = JsonSerializer.Deserialize<List<string>>(cleanedResponse);

            return trackIds != null ? new HashSet<string>(trackIds) : [];
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI response as JSON. Response: {Response}", response);

            return [];
        }
    }

    /// <summary>
    /// Generates a suggested playlist name based on the user's filtering prompt.
    /// </summary>
    public async Task<string> GeneratePlaylistNameAsync(
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = ObservabilityExtensions.StartActivity("AIGeneratePlaylistName");
        activity?.SetTag("ai.prompt", userPrompt);
        activity?.SetTag("ai.system_instructions", _systemInstructions);
        activity?.SetTag("ai.model", _ollamaClient.SelectedModel);

        try
        {
            string namePrompt = $"""
                Based on this music filtering request: "{userPrompt}"

                Generate a short, catchy playlist name (max 50 characters) that captures the essence of this filter.
                Respond ONLY with the playlist name, nothing else. Do not use quotes.
                Examples: "2000s Upbeat Hits", "Chill Acoustic Vibes", "Energetic Workout Mix"
                """;

            activity?.SetTag("ai.full_prompt", namePrompt);
            activity?.SetTag("ai.full_prompt_length", namePrompt.Length);

            ChatRequest chatRequest = new ChatRequest
            {
                Messages = new List<Message>
                {
                    new Message { Role = "system", Content = _systemInstructions },
                    new Message { Role = "user", Content = namePrompt }
                }
            };

            string response = string.Empty;
            await foreach (ChatResponseStream? stream in _ollamaClient.Chat(chatRequest, cancellationToken))
            {
                if (stream?.Message?.Content != null)
                {
                    response += stream.Message.Content;
                }
            }

            activity?.SetTag("ai.response", response);
            activity?.SetTag("ai.response_length", response.Length);

            string playlistName = response.Trim().Trim('"', '\'');

            // Ensure it's not too long
            if (playlistName.Length > 50)
            {
                playlistName = playlistName[..47] + "...";
            }

            _logger.LogInformation("Generated playlist name: {Name} for prompt: {Prompt}", playlistName, userPrompt);

            return playlistName;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate playlist name, using default");

            return "AI Filtered Playlist";
        }
    }
}

/// <summary>
/// Simplified track representation for AI analysis.
/// </summary>
internal sealed class TrackFilterDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("artist")]
    public string Artist { get; init; } = string.Empty;

    [JsonPropertyName("album")]
    public string Album { get; init; } = string.Empty;

    [JsonPropertyName("year")]
    public int? Year { get; init; }

    [JsonPropertyName("genre")]
    public string Genre { get; init; } = string.Empty;

    [JsonPropertyName("energy")]
    public float Energy { get; init; }

    [JsonPropertyName("valence")]
    public float Valence { get; init; }

    [JsonPropertyName("danceability")]
    public float Danceability { get; init; }

    [JsonPropertyName("tempo")]
    public float Tempo { get; init; }

    [JsonPropertyName("acousticness")]
    public float Acousticness { get; init; }

    [JsonPropertyName("instrumentalness")]
    public float Instrumentalness { get; init; }

    [JsonPropertyName("mood")]
    public string Mood { get; init; } = string.Empty;
}