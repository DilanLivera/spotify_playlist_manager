using System.Diagnostics;
using System.Text;

namespace UI.Infrastructure.Observability;

/// <summary>
/// HTTP message handler that logs request and response details to OpenTelemetry traces.
/// Captures method, URL, headers, body content, and response details.
/// </summary>
public sealed class HttpLoggingHandler : DelegatingHandler
{
    private readonly ILogger<HttpLoggingHandler> _logger;
    private readonly HttpLoggingOptions _options;

    public HttpLoggingHandler(
        ILogger<HttpLoggingHandler> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _options = configuration.GetSection("Observability:HttpLogging")
            .Get<HttpLoggingOptions>() ?? new HttpLoggingOptions();
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        using Activity? activity = ObservabilityExtensions.StartActivity("HttpRequest");

        // Log request details
        await EnrichActivityWithRequest(activity, request);

        // Execute the request
        Stopwatch stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
        stopwatch.Stop();

        // Log response details
        await EnrichActivityWithResponse(activity, response, stopwatch.ElapsedMilliseconds);

        return response;
    }

    private async Task EnrichActivityWithRequest(Activity? activity, HttpRequestMessage request)
    {
        if (activity == null)
            return;

        try
        {
            // Basic request info
            activity.SetTag("http.method", request.Method.ToString());
            activity.SetTag("http.url", request.RequestUri?.ToString());
            activity.SetTag("http.scheme", request.RequestUri?.Scheme);
            activity.SetTag("http.host", request.RequestUri?.Host);
            activity.SetTag("http.path", request.RequestUri?.AbsolutePath);

            // Request headers (excluding sensitive ones)
            if (_options.LogHeaders)
            {
                StringBuilder headersBuilder = new();
                foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers.Where(h => !IsSensitiveHeader(h.Key)))
                {
                    headersBuilder.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
                }
                if (headersBuilder.Length > 0)
                {
                    activity.SetTag("http.request.headers", headersBuilder.ToString().TrimEnd());
                }
            }

            // Request body (if present)
            if (_options.LogRequestBody && request.Content != null)
            {
                // Read the content without consuming it
                string requestBody = await ReadContentAsync(request.Content);

                if (!string.IsNullOrEmpty(requestBody))
                {
                    string truncatedBody = TruncateIfNeeded(requestBody, _options.MaxBodyLength);
                    activity.SetTag("http.request.body", truncatedBody);
                    activity.SetTag("http.request.body_length", requestBody.Length);

                    if (requestBody.Length > _options.MaxBodyLength)
                    {
                        activity.SetTag("http.request.body_truncated", true);
                    }
                }
            }

            _logger.LogDebug("HTTP Request: {Method} {Url}", request.Method, request.RequestUri);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enrich activity with request details");

            Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);

            throw;
        }
    }

    private async Task EnrichActivityWithResponse(
        Activity? activity,
        HttpResponseMessage response,
        long elapsedMs)
    {
        if (activity == null)
            return;

        try
        {
            // Response status
            activity.SetTag("http.status_code", (int)response.StatusCode);
            activity.SetTag("http.status_text", response.ReasonPhrase);
            activity.SetTag("http.duration_ms", elapsedMs);

            // Response headers
            if (_options.LogHeaders)
            {
                StringBuilder headersBuilder = new();
                foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers)
                {
                    headersBuilder.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
                }
                if (headersBuilder.Length > 0)
                {
                    activity.SetTag("http.response.headers", headersBuilder.ToString().TrimEnd());
                }
            }

            // Response body
            if (_options.LogResponseBody && response.Content != null)
            {
                // Read the content and replace it so downstream can still read it
                string responseBody = await ReadAndReplaceContentAsync(response);

                if (!string.IsNullOrEmpty(responseBody))
                {
                    string truncatedBody = TruncateIfNeeded(responseBody, _options.MaxBodyLength);
                    activity.SetTag("http.response.body", truncatedBody);
                    activity.SetTag("http.response.body_length", responseBody.Length);

                    if (responseBody.Length > _options.MaxBodyLength)
                    {
                        activity.SetTag("http.response.body_truncated", true);
                    }
                }
            }

            // Set activity status based on HTTP status
            if (!response.IsSuccessStatusCode)
            {
                activity.SetStatus(ActivityStatusCode.Error, $"HTTP {(int)response.StatusCode}");
            }

            _logger.LogDebug("HTTP Response: {StatusCode} in {Duration}ms",
                (int)response.StatusCode, elapsedMs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enrich activity with response details");

            Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);

            throw;
        }
    }

    /// <summary>
    /// Reads content without consuming the stream (for request bodies).
    /// </summary>
    private static async Task<string> ReadContentAsync(HttpContent content)
    {
        // For requests, we can read the content as-is since it will be sent later
        byte[] buffer = await content.ReadAsByteArrayAsync();
        return Encoding.UTF8.GetString(buffer);
    }

    /// <summary>
    /// Reads response content and replaces it so downstream consumers can still read it.
    /// </summary>
    private static async Task<string> ReadAndReplaceContentAsync(HttpResponseMessage response)
    {
        // Read the original content
        string content = await response.Content.ReadAsStringAsync();

        // Replace the content with a new StringContent so it can be read again downstream
        string? mediaType = response.Content.Headers.ContentType?.MediaType;
        response.Content = new StringContent(content, Encoding.UTF8, mediaType ?? "application/json");

        // Copy over the original headers
        foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers.ToList())
        {
            response.Content.Headers.Remove(header.Key);
            response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return content;
    }

    private static string TruncateIfNeeded(string content, int maxLength)
    {
        if (content.Length <= maxLength)
        {
            return content;
        }

        return content[..maxLength] + $"\n... (truncated {content.Length - maxLength} characters)";
    }

    private static bool IsSensitiveHeader(string headerName)
    {
        string[] sensitiveHeaders =
        [
            "Authorization",
            "Cookie",
            "Set-Cookie",
            "X-API-Key",
            "X-Auth-Token"
        ];

        return sensitiveHeaders.Contains(headerName, StringComparer.OrdinalIgnoreCase);
    }
}