using Callora.Core.Application.Http.Contracts;
using System.Text.Json;

namespace Callora.Core.Infrastructure.Http;

/// <summary>
/// Server-sent-events writer over the ASP.NET response.
/// </summary>
public sealed class HostApiEventStream(HttpContext httpContext) : ApiEventStream
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override CancellationToken Aborted => httpContext.RequestAborted;

    public override async Task WriteEventAsync(object payload, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await httpContext.Response.WriteAsync($"data: {json}\n\n", cancellationToken).ConfigureAwait(false);
        await httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
