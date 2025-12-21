using System.Text.Json.Serialization;

namespace UI.Infrastructure.Spotify;

/// <summary>
/// Response wrapper for ReccoBeats audio features endpoint.
/// </summary>
public sealed class ReccoBeatsAudioFeaturesResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("data")]
    public ReccoBeatsAudioFeatures? Data { get; init; }
}

/// <summary>
/// Audio features data from ReccoBeats API.
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

