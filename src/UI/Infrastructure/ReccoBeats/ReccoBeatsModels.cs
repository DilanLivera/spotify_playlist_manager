using System.Text.Json.Serialization;

namespace UI.Infrastructure.ReccoBeats;

/// <summary>
/// Response from ReccoBeats track lookup endpoint.
/// </summary>
public sealed class ReccoBeatsTrackLookupResponse
{
    [JsonPropertyName("content")]
    public List<ReccoBeatsTrackInfo> Content { get; init; } = [];
}

/// <summary>
/// Track information from ReccoBeats API.
/// </summary>
public sealed class ReccoBeatsTrackInfo
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("trackTitle")]
    public string TrackTitle { get; init; } = string.Empty;

    [JsonPropertyName("href")]
    public string Href { get; init; } = string.Empty;
}

/// <summary>
/// Audio features data from ReccoBeats API.
/// Returned directly from the /v1/track/:id/audio-features endpoint.
/// </summary>
public sealed class ReccoBeatsAudioFeatures
{
    [JsonPropertyName("acousticness")]
    public float Acousticness { get; init; }

    [JsonPropertyName("danceability")]
    public float Danceability { get; init; }

    [JsonPropertyName("energy")]
    public float Energy { get; init; }

    [JsonPropertyName("instrumentalness")]
    public float Instrumentalness { get; init; }

    [JsonPropertyName("key")]
    public int Key { get; init; }

    [JsonPropertyName("liveness")]
    public float Liveness { get; init; }

    [JsonPropertyName("loudness")]
    public float Loudness { get; init; }

    [JsonPropertyName("mode")]
    public int Mode { get; init; }

    [JsonPropertyName("speechiness")]
    public float Speechiness { get; init; }

    [JsonPropertyName("tempo")]
    public float Tempo { get; init; }

    [JsonPropertyName("valence")]
    public float Valence { get; init; }
}