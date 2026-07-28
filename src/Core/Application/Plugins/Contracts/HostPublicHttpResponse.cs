using System.Net;
using System.Net.Mime;

namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Response model returned by plugin-provided public HTTP route handlers.
/// The host writes exactly these values to the HTTP connection; no additional
/// headers or body content is appended.
/// </summary>
/// <param name="StatusCode">HTTP status code (for example: <c>200</c>, <c>302</c>, <c>404</c>).</param>
/// <param name="ContentType">
/// MIME content type of <see cref="Body"/> (for example: <c>text/html; charset=utf-8</c>
/// or <c>application/json</c>).
/// </param>
/// <param name="Body">Response body string written verbatim to the response stream.</param>
/// <param name="Headers">
/// Optional additional response headers to include (for example:
/// <c>Location</c> for redirects). When <c>null</c>, no extra headers are written.
/// </param>
public sealed record HostPublicHttpResponse(
    int StatusCode,
    string ContentType,
    string Body,
    IReadOnlyDictionary<string, string>? Headers = null)
{
    /// <summary>
    /// Creates a 200 OK response with an HTML body (<c>text/html; charset=utf-8</c>).
    /// </summary>
    /// <param name="html">The HTML document string.</param>
    public static HostPublicHttpResponse Html(string html) =>
        new((int)HttpStatusCode.OK, "text/html; charset=utf-8", html);

    /// <summary>
    /// Creates a 200 OK response with a JSON body (<c>application/json</c>).
    /// </summary>
    /// <param name="json">The JSON string.</param>
    public static HostPublicHttpResponse Json(string json) =>
        new((int)HttpStatusCode.OK, MediaTypeNames.Application.Json, json);

    /// <summary>
    /// Creates a 302 Found redirect response with the given <paramref name="location"/>.
    /// The <c>Location</c> header is populated from the argument; <see cref="Body"/> is empty.
    /// </summary>
    /// <param name="location">The absolute or relative URL to redirect to.</param>
    public static HostPublicHttpResponse Redirect(string location) =>
        new(
            (int)HttpStatusCode.Found,
            "text/plain",
            string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Location"] = location
            });

    /// <summary>
    /// Creates a 404 Not Found response with an empty body.
    /// </summary>
    public static HostPublicHttpResponse NotFound() =>
        new((int)HttpStatusCode.NotFound, "text/plain", string.Empty);
}
