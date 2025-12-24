using UI.Infrastructure.Persistence;

namespace UI.Infrastructure.Persistence;

/// <summary>
/// Extension methods for registering persistence-related services.
/// </summary>
public static class PersistenceExtensions
{
    /// <summary>
    /// Adds persistence services to the application.
    /// </summary>
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services)
    {
        // Register TrackCacheService as a Singleton since the cache is shared across the app
        services.AddSingleton<TrackCacheService>();
        
        return services;
    }
}

