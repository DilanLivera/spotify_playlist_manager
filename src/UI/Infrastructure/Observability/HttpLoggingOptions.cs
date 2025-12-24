namespace UI.Infrastructure.Observability;

/// <summary>
/// Configuration options for HTTP request/response logging.
/// </summary>
public sealed record HttpLoggingOptions
{
    /// <summary>
    /// Gets whether HTTP logging is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets the maximum length of request/response bodies to log.
    /// Content exceeding this length will be truncated.
    /// </summary>
    public int MaxBodyLength { get; init; } = 5000;

    /// <summary>
    /// Gets whether to log HTTP headers.
    /// </summary>
    public bool LogHeaders { get; init; } = true;

    /// <summary>
    /// Gets whether to log request bodies.
    /// </summary>
    public bool LogRequestBody { get; init; } = true;

    /// <summary>
    /// Gets whether to log response bodies.
    /// </summary>
    public bool LogResponseBody { get; init; } = true;
}