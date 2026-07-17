namespace Callora.Core;

/// <summary>
/// Base for Callora domain exceptions: a fault a caller can reasonably anticipate and
/// handle, carrying a stable machine-readable <see cref="ErrorCode"/> and the HTTP
/// <see cref="StatusCode"/> it maps to. The central exception handler renders these as
/// RFC 9457 problem responses so a client can branch on the code — the Callora counterpart
/// of Shopware's HttpException-based domain exceptions.
/// </summary>
/// <remarks>
/// Reserve this for expected faults (validation, not-found, policy rejections). Programmer
/// errors — broken invariants, misconfiguration, "this cannot happen" guards — stay plain
/// <see cref="InvalidOperationException"/> and surface as a generic 500.
/// </remarks>
public abstract class CalloraException : Exception
{
    /// <summary>Creates the exception with its stable code, HTTP status and detail.</summary>
    /// <param name="errorCode">Stable machine-readable code, e.g. "WEBHOOK__TARGET_BLOCKED".</param>
    /// <param name="statusCode">HTTP status this fault maps to.</param>
    /// <param name="message">Human-readable detail.</param>
    /// <param name="innerException">Optional underlying cause.</param>
    protected CalloraException(string errorCode, int statusCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    /// <summary>Stable machine-readable error code, conventionally <c>DOMAIN__REASON</c>.</summary>
    public string ErrorCode { get; }

    /// <summary>HTTP status this fault maps to.</summary>
    public int StatusCode { get; }
}
