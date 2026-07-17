namespace Callora.Core.Api;

/// <summary>
/// RFC 9457 problem responses with stable callora problem types (PLAT-210).
/// All error responses go through this helper so clients can rely on the
/// application/problem+json shape (type, title, status, detail).
/// </summary>
public static class ApiProblems
{
    /// <summary>
    /// Base URI prepended to problem-type slugs. Defaults to a URN so no
    /// registered domain is required; override via
    /// BackendHost:ProblemTypeBaseUri (e.g. "https://docs.example.com/problems/")
    /// once a documentation host exists. Should end with ":" or "/".
    /// </summary>
    public static string TypeBaseUri { get; set; } = "urn:callora:problem:";

    public static IResult BadRequest(string detail) =>
        Build(StatusCodes.Status400BadRequest, "Bad Request", "bad-request", detail);

    public static IResult NotFound(string detail) =>
        Build(StatusCodes.Status404NotFound, "Not Found", "not-found", detail);

    public static IResult Conflict(string detail) =>
        Build(StatusCodes.Status409Conflict, "Conflict", "conflict", detail);

    public static IResult UnprocessableEntity(string detail) =>
        Build(StatusCodes.Status422UnprocessableEntity, "Unprocessable Entity", "unprocessable-entity", detail);

    public static IResult ServiceUnavailable(string detail) =>
        Build(StatusCodes.Status503ServiceUnavailable, "Service Unavailable", "service-unavailable", detail);

    /// <summary>
    /// Renders a <see cref="CalloraException"/> as a problem response, carrying its stable
    /// error code both as the problem type and as a machine-readable <c>code</c> member so a
    /// client can branch on it.
    /// </summary>
    public static IResult FromException(CalloraException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return Results.Problem(
            detail: exception.Message,
            statusCode: exception.StatusCode,
            type: TypeBaseUri + exception.ErrorCode,
            extensions: new Dictionary<string, object?> { ["code"] = exception.ErrorCode });
    }

    private static IResult Build(int statusCode, string title, string typeSlug, string detail) =>
        Results.Problem(
            detail: detail,
            statusCode: statusCode,
            title: title,
            type: TypeBaseUri + typeSlug);
}
