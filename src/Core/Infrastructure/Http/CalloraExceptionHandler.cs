using Callora.Core.Api;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Callora.Core.Infrastructure.Http;

/// <summary>
/// Maps a <see cref="CalloraException"/> to an RFC 9457 problem response via
/// <see cref="ApiProblems.FromException"/>. Any other exception is left to the default
/// handling (500), so programmer errors stay visible as server errors rather than being
/// dressed up as expected faults.
/// </summary>
public sealed class CalloraExceptionHandler(ILogger<CalloraExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not CalloraException calloraException)
        {
            return false;
        }

        logger.LogWarning(exception, "Handled domain exception {ErrorCode}.", calloraException.ErrorCode);
        await ApiProblems.FromException(calloraException).ExecuteAsync(httpContext).ConfigureAwait(false);
        return true;
    }
}
