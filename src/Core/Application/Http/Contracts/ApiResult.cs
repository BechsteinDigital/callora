namespace Callora.Core.Application.Http.Contracts;

/// <summary>
/// Transport-neutral action result. Plugin controllers return it; the host
/// serializes bodies as JSON and problems as RFC 9457 responses.
/// </summary>
public sealed class ApiResult
{
    private ApiResult(int statusCode, object? body, string? location, ApiProblemPayload? problem)
    {
        StatusCode = statusCode;
        Body = body;
        Location = location;
        Problem = problem;
    }

    /// <summary>HTTP status code.</summary>
    public int StatusCode { get; }

    /// <summary>JSON-serialized response body; null for empty responses.</summary>
    public object? Body { get; }

    /// <summary>Location header value for created resources.</summary>
    public string? Location { get; }

    /// <summary>Problem payload; set for error results.</summary>
    public ApiProblemPayload? Problem { get; }

    /// <summary>200 with an optional body.</summary>
    public static ApiResult Ok(object? body = null) => new(200, body, null, null);

    /// <summary>201 with Location header and body.</summary>
    public static ApiResult Created(string location, object? body = null) => new(201, body, location, null);

    /// <summary>204 without body.</summary>
    public static ApiResult NoContent() => new(204, null, null, null);

    /// <summary>400 problem.</summary>
    public static ApiResult BadRequest(string detail) => FromProblem(400, "Bad Request", detail);

    /// <summary>403 without body.</summary>
    public static ApiResult Forbidden() => new(403, null, null, null);

    /// <summary>404 problem.</summary>
    public static ApiResult NotFound(string detail) => FromProblem(404, "Not Found", detail);

    /// <summary>409 problem.</summary>
    public static ApiResult Conflict(string detail) => FromProblem(409, "Conflict", detail);

    /// <summary>Arbitrary problem result.</summary>
    public static ApiResult FromProblem(int status, string title, string detail) =>
        new(status, null, null, new ApiProblemPayload(status, title, detail));
}
