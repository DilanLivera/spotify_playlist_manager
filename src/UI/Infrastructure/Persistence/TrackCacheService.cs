using System.Text.Json;
using Microsoft.Data.Sqlite;
using UI.Infrastructure.ReccoBeats;

namespace UI.Infrastructure.Persistence;

/// <summary>
/// Service for caching ReccoBeats audio features in a local SQLite database.
/// Improves performance by avoiding redundant API calls.
/// </summary>
public sealed class TrackCacheService
{
    private readonly string _connectionString;
    private readonly ILogger<TrackCacheService> _logger;

    public TrackCacheService(IConfiguration configuration, ILogger<TrackCacheService> logger)
    {
        _connectionString = configuration.GetConnectionString("TrackCache") ?? "Data Source=tracks_cache.db";
        _logger = logger;
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        try
        {
            using SqliteConnection connection = new(_connectionString);
            connection.Open();

            using SqliteCommand command = connection.CreateCommand();

            // Enable WAL mode for better concurrency and performance
            command.CommandText = "PRAGMA journal_mode=WAL;";
            command.ExecuteNonQuery();

            // Create the cache table if it doesn't exist
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS ReccoBeatsCache (
                    SpotifyTrackId TEXT PRIMARY KEY,
                    JsonData TEXT NOT NULL,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );

                CREATE INDEX IF NOT EXISTS IX_ReccoBeatsCache_SpotifyTrackId ON ReccoBeatsCache(SpotifyTrackId);
            """;
            command.ExecuteNonQuery();

            _logger.LogInformation("SQLite cache initialized successfully with connection string: {ConnectionString}", _connectionString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SQLite cache database");
            throw;
        }
    }

    /// <summary>
    /// Retrieves cached audio features for a set of track IDs in bulk.
    /// Handles batching to avoid SQLite parameter limits.
    /// </summary>
    public async Task<Dictionary<string, ReccoBeatsAudioFeatures>> GetCachedFeaturesAsync(string[] trackIds, CancellationToken ct)
    {
        Dictionary<string, ReccoBeatsAudioFeatures> results = new();

        if (trackIds.Length == 0)
        {
            return results;
        }

        const int batchSize = 500;

        try
        {
            await using SqliteConnection connection = new(_connectionString);
            await connection.OpenAsync(ct);

            for (int i = 0; i < trackIds.Length; i += batchSize)
            {
                string[] batch = trackIds.Skip(i).Take(batchSize).ToArray();
                string parameterNames = string.Join(",", batch.Select((_, idx) => $"@id{i + idx}"));

                await using SqliteCommand command = connection.CreateCommand();
                command.CommandText = $"SELECT SpotifyTrackId, JsonData FROM ReccoBeatsCache WHERE SpotifyTrackId IN ({parameterNames})";

                for (int idx = 0; idx < batch.Length; idx++)
                {
                    command.Parameters.AddWithValue($"@id{i + idx}", batch[idx]);
                }

                await using SqliteDataReader reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    string trackId = reader.GetString(0);
                    string json = reader.GetString(1);

                    try
                    {
                        ReccoBeatsAudioFeatures? features = JsonSerializer.Deserialize<ReccoBeatsAudioFeatures>(json);
                        if (features != null)
                        {
                            results[trackId] = features;
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize cached features for track {TrackId}", trackId);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error retrieving features from SQLite cache");
        }

        return results;
    }

    /// <summary>
    /// Saves audio features for a single track to the cache.
    /// </summary>
    public async Task SaveFeaturesAsync(string trackId, ReccoBeatsAudioFeatures features, CancellationToken ct)
    {
        try
        {
            await using SqliteConnection connection = new(_connectionString);
            await connection.OpenAsync(ct);

            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR REPLACE INTO ReccoBeatsCache (SpotifyTrackId, JsonData)
                VALUES (@id, @json)
            """;
            command.Parameters.AddWithValue("@id", trackId);
            command.Parameters.AddWithValue("@json", JsonSerializer.Serialize(features));

            await command.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error saving features to SQLite cache for track {TrackId}", trackId);
        }
    }
}