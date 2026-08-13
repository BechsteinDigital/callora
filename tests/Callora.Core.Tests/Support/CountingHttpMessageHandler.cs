using System.Net;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Zählt Aufrufe und beantwortet sie nach einer Regel — für Tests, in denen die ANZAHL der
/// Versuche die Aussage ist.
/// </summary>
/// <remarks>
/// Getrennt von <see cref="RecordingHttpMessageHandler"/>, weil der die Anfragen aufhebt: Unter
/// einer Wiederholungskette werden dieselben Objekte mehrfach gesendet und danach verworfen, und
/// eine aufgehobene <see cref="HttpRequestMessage"/> ist dann bereits entsorgt. Gezählt wird
/// threadsicher, weil die Kette parallel senden darf.
/// </remarks>
public sealed class CountingHttpMessageHandler(Func<HttpRequestMessage, HttpStatusCode> respond) : HttpMessageHandler
{
    private int _count;

    public CountingHttpMessageHandler(HttpStatusCode statusCode)
        : this(_ => statusCode)
    {
    }

    public int Count => Volatile.Read(ref _count);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _count);
        return Task.FromResult(new HttpResponseMessage(respond(request)));
    }
}
