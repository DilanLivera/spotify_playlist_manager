using OllamaSharp;

namespace UI.Infrastructure.AIAgent;

/// <summary>
/// Extension methods for configuring AI Agent services.
/// </summary>
public static class AiAgentExtensions
{
    /// <summary>
    /// Adds AI Agent services to the application.
    /// Configures Ollama client for natural language track filtering.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiAgentServices(this IServiceCollection services, IConfiguration configuration)
    {
        string ollamaEndpoint = configuration["AIAgent:OllamaEndpoint"] ?? throw new Exception("'AIAgent:OllamaEndpoint' configuration value is missing");
        string modelName = configuration["AIAgent:ModelName"] ?? throw new Exception("'AIAgent:ModelName' configuration value is missing");

        services.AddSingleton<OllamaApiClient>(serviceProvider =>
        {
            ILogger<OllamaApiClient> logger = serviceProvider.GetRequiredService<ILogger<OllamaApiClient>>();
            IHttpClientFactory clientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            HttpClient httpClient = clientFactory.CreateClient(nameof(OllamaApiClient));
            httpClient.BaseAddress = new Uri(ollamaEndpoint);
            httpClient.Timeout = TimeSpan.FromMinutes(30);

            OllamaApiClient client = new(httpClient, defaultModel: modelName);

            logger.LogInformation("Configured Ollama client with endpoint: {Endpoint}, model: {Model}",
                                  ollamaEndpoint,
                                  modelName);

            return client;
        });

        services.AddScoped<AiTrackFilterService>();

        return services;
    }
}