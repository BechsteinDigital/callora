using System.Net;

namespace Callora.Core.Tests.Support;

/// <summary>
/// HTTP handler fake recording outgoing requests and answering with a
/// configurable status code.
/// </summary>
public sealed class RecordingHttpMessageHandler(HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
{
    private readonly List<(HttpRequestMessage Request, string Body)> _requests = [];

    public IReadOnlyList<(HttpRequestMessage Request, string Body)> Requests => _requests;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);
        _requests.Add((request, body));
        return new HttpResponseMessage(statusCode);
    }
}
