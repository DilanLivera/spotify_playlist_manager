using Azure;
using Azure.AI.OpenAI;
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
        services.AddSingleton<IChatClient>(serviceProvider =>
        {
            ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            ILogger logger = loggerFactory.CreateLogger(categoryName: "AIChat");

            string? azureOpenAiEndpoint = configuration["AIChat:AzureOpenAI:Endpoint"];
            string? azureKeyCredential = configuration["AIChat:AzureOpenAI:AzureKeyCredential"];
            string? azureOpenAiModelName = configuration["AIChat:AzureOpenAI:Model"];

            if (!string.IsNullOrWhiteSpace(azureOpenAiEndpoint) &&
                !string.IsNullOrWhiteSpace(azureKeyCredential) &&
                !string.IsNullOrWhiteSpace(azureOpenAiModelName))
            {
                Uri endpoint = new(azureOpenAiEndpoint);
                AzureKeyCredential credential = new(azureKeyCredential);
                AzureOpenAIClient client = new(endpoint, credential);
                IChatClient openAiClient = client.GetChatClient(deploymentName: azureOpenAiModelName)
                                                 .AsIChatClient();

                logger.LogInformation("Configured Azure OpenAI client with endpoint: {Endpoint}, model: {Model}",
                                      azureOpenAiEndpoint,
                                      azureOpenAiModelName);

                return openAiClient;
            }

            string? ollamaEndpoint = configuration["AIChat:OllamaEndpoint"];
            string? modelName = configuration["AIChat:ModelName"];

            if (string.IsNullOrWhiteSpace(ollamaEndpoint) || string.IsNullOrWhiteSpace(modelName))
            {
                throw new InvalidOperationException("""
                                                    Incomplete configuration for AI Chat. At least one of the following must be fully configured:
                                                    Azure OpenAI (AIChat:AzureOpenAI:Endpoint, AIChat:AzureOpenAI:AzureKeyCredential, AIChat:AzureOpenAI:Model)
                                                    or Ollama (AIChat:OllamaEndpoint, AIChat:ModelName).
                                                    """);
            }

            IHttpClientFactory clientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            HttpClient httpClient = clientFactory.CreateClient(nameof(OllamaApiClient));
            httpClient.BaseAddress = new Uri(ollamaEndpoint);
            httpClient.Timeout = TimeSpan.FromMinutes(30);

            IChatClient ollamaClient = new OllamaApiClient(httpClient, defaultModel: modelName);

            logger.LogInformation("Configured Ollama client with endpoint: {Endpoint}, model: {Model}",
                                  ollamaEndpoint,
                                  modelName);

            return ollamaClient;
        });

        services.AddScoped<AiTrackFilterService>();

        return services;
    }
}