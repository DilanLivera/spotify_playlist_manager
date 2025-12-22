using OllamaSharp;

namespace UI.Infrastructure.AIAgent;

/// <summary>
/// Extension methods for configuring AI Agent services.
/// </summary>
public static class AIAgentExtensions
{
    /// <summary>
    /// Adds AI Agent services to the application.
    /// Configures Ollama client for natural language track filtering.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAIAgentServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Get configuration values
        string ollamaEndpoint = configuration["AIAgent:OllamaEndpoint"] ?? "http://localhost:11434";
        string modelName = configuration["AIAgent:ModelName"] ?? "llama3";

        // Register OllamaApiClient as a singleton
        services.AddSingleton<OllamaApiClient>(serviceProvider =>
        {
            ILogger<OllamaApiClient> logger = serviceProvider.GetRequiredService<ILogger<OllamaApiClient>>();
            
            try
            {
                OllamaApiClient client = new OllamaApiClient(new Uri(ollamaEndpoint), modelName);
                logger.LogInformation("Configured Ollama client with endpoint: {Endpoint}, model: {Model}",
                                      ollamaEndpoint,
                                      modelName);

                return client;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create Ollama client. Ensure Ollama is running at {Endpoint}", ollamaEndpoint);

                throw;
            }
        });

        // Register the AITrackFilterService
        services.AddScoped<AITrackFilterService>();

        return services;
    }
}
