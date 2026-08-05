using System.Text;
using Microsoft.AspNetCore.Http;

namespace Callora.Administration.Api;

/// <summary>
/// Reads a request body under a hard byte cap. Both plugin-facing seams need it and
/// need it the same way: a declared <c>Content-Length</c> is a hint a client controls,
/// and a chunked request declares none at all, so the cap has to hold on the read
/// itself rather than on what the request claims.
/// </summary>
internal static class CappedRequestBody
{
    /// <summary>
    /// Reads the body as UTF-8 text, or reports that it exceeded the cap.
    /// </summary>
    /// <param name="httpContext">Request whose body to read.</param>
    /// <param name="limitBytes">Maximum number of bytes to accept.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The body text (null when empty) and whether the cap was exceeded.</returns>
    public static async Task<(string? Body, bool TooLarge)> ReadAsync(
        HttpContext httpContext,
        int limitBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // Cheap rejection for an honestly oversized request; the capped read below is
        // what actually holds when the declaration is missing or lies.
        if (httpContext.Request.ContentLength > limitBytes)
        {
            return (null, true);
        }

        var buffer = new byte[8 * 1024];
        using var accumulated = new MemoryStream();

        int read;
        while ((read = await httpContext.Request.Body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (accumulated.Length + read > limitBytes)
            {
                return (null, true);
            }

            accumulated.Write(buffer, 0, read);
        }

        return accumulated.Length == 0
            ? (null, false)
            : (Encoding.UTF8.GetString(accumulated.ToArray()), false);
    }
}
