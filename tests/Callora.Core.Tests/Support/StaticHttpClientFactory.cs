namespace Callora.Core.Tests.Support;

/// <summary>
/// IHttpClientFactory fake returning clients over one shared handler.
/// </summary>
public sealed class StaticHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}
