using Microsoft.Data.Sqlite;
using System.Text.Json;
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
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            
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
        var results = new Dictionary<string, ReccoBeatsAudioFeatures>();
        
        if (trackIds.Length == 0)
        {
            return results;
        }

        const int batchSize = 500;
        
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            for (int i = 0; i < trackIds.Length; i += batchSize)
            {
                var batch = trackIds.Skip(i).Take(batchSize).ToArray();
                var parameterNames = string.Join(",", batch.Select((_, idx) => $"@id{i + idx}"));
                
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT SpotifyTrackId, JsonData FROM ReccoBeatsCache WHERE SpotifyTrackId IN ({parameterNames})";
                
                for (int idx = 0; idx < batch.Length; idx++)
                {
                    command.Parameters.AddWithValue($"@id{i + idx}", batch[idx]);
                }

                using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var trackId = reader.GetString(0);
                    var json = reader.GetString(1);
                    
                    try
                    {
                        var features = JsonSerializer.Deserialize<ReccoBeatsAudioFeatures>(json);
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
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            using var command = connection.CreateCommand();
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

