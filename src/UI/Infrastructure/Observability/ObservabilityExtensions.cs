using System.Diagnostics;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace UI.Infrastructure.Observability;

public static class ObservabilityExtensions
{
    public const string ServiceName = "SpotifyPlaylistManager";

    private static readonly ActivitySource ActivitySource = new(ServiceName);

    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration)
    {
        Action<ResourceBuilder> configureResource = resource => resource
            .AddService(
                serviceName: ServiceName,
                serviceVersion: typeof(ObservabilityExtensions).Assembly.GetName().Version?.ToString() ?? "1.0.0");

        services.AddOpenTelemetry()
                .ConfigureResource(configureResource)
                .WithTracing(tracing =>
                {
                    tracing.AddAspNetCoreInstrumentation(options =>
                           {
                               options.RecordException = true;
                               options.Filter = httpContext =>
                               {
                                   // Exclude static files and health checks from tracing
                                   string path = httpContext.Request.Path.Value ?? "";

                                   return !path.StartsWith("/_") &&
                                          !path.StartsWith("/css") &&
                                          !path.StartsWith("/js") &&
                                          !path.StartsWith("/favicon");
                               };
                           })
                           .AddHttpClientInstrumentation(options =>
                           {
                               options.RecordException = true;
                               options.FilterHttpRequestMessage = request => request.RequestUri?.Host.Contains("spotify") == true;
                               options.EnrichWithHttpRequestMessage = (activity, request) =>
                               {
                                   activity.SetTag("spotify.endpoint", request.RequestUri?.AbsolutePath);
                               };
                               options.EnrichWithHttpResponseMessage = (activity, response) =>
                               {
                                   activity.SetTag("spotify.status_code", (int)response.StatusCode);
                               };
                           })
                           .AddSource(ServiceName);

                    // Configure exporter based on environment
                    string? otlpEndpoint = configuration["Observability:OtlpEndpoint"];
                    if (!string.IsNullOrEmpty(otlpEndpoint))
                    {
                        tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
                    }
                    else
                    {
                        tracing.AddConsoleExporter();
                    }
                })
                .WithMetrics(metrics =>
                {
                    metrics.AddAspNetCoreInstrumentation()
                           .AddHttpClientInstrumentation()
                           .AddMeter(ServiceName);

                    string? otlpEndpoint = configuration["Observability:OtlpEndpoint"];
                    if (!string.IsNullOrEmpty(otlpEndpoint))
                    {
                        metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
                    }
                    else
                    {
                        metrics.AddConsoleExporter();
                    }
                });

        return services;
    }

    public static ILoggingBuilder AddObservabilityLogging(this ILoggingBuilder logging, IConfiguration configuration)
    {
        logging.AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
            options.ParseStateValues = true;

            string? otlpEndpoint = configuration["Observability:OtlpEndpoint"];
            if (!string.IsNullOrEmpty(otlpEndpoint))
            {
                options.AddOtlpExporter(exporterOptions => exporterOptions.Endpoint = new Uri(otlpEndpoint));
            }
            else
            {
                options.AddConsoleExporter();
            }
        });

        return logging;
    }

    /// <summary>
    /// Starts a new activity for custom tracing within application code.
    /// </summary>
    public static Activity? StartActivity(string name) => ActivitySource.StartActivity(name);
}

