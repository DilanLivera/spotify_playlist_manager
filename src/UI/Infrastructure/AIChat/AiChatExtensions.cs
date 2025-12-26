using Microsoft.Extensions.AI;
using OllamaSharp;

namespace UI.Infrastructure.AIChat;

/// <summary>
/// Extension methods for configuring AI Chat services.
/// </summary>
public static class AiChatExtensions
{
    /// <summary>
    /// Adds AI services to the application.
    /// Configures AI client for natural language track filtering.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiChatServices(this IServiceCollection services, IConfiguration configuration)
    {
        string ollamaEndpoint = configuration["AIChat:OllamaEndpoint"] ?? throw new Exception("'AIChat:OllamaEndpoint' configuration value is missing");
        string modelName = configuration["AIChat:ModelName"] ?? throw new Exception("'AIChat:ModelName' configuration value is missing");

        services.AddSingleton<IChatClient>(serviceProvider =>
        {
            ILogger<OllamaApiClient> logger = serviceProvider.GetRequiredService<ILogger<OllamaApiClient>>();
            IHttpClientFactory clientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            HttpClient httpClient = clientFactory.CreateClient(nameof(OllamaApiClient));
            httpClient.BaseAddress = new Uri(ollamaEndpoint);
            httpClient.Timeout = TimeSpan.FromMinutes(30);

            IChatClient client = new OllamaApiClient(httpClient, defaultModel: modelName);

            logger.LogInformation("Configured Ollama client with endpoint: {Endpoint}, model: {Model}",
                                  ollamaEndpoint,
                                  modelName);

            return client;
        });

        services.AddScoped<AiTrackFilterService>();

        return services;
    }
}