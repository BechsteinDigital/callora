using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Surfaces;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Host identity source returning a fixed result — stands in for "an operator is
/// already signed in at the backend" without an <c>HttpContext</c>.
/// </summary>
public sealed class StubSurfaceHostIdentitySource(HostSurfaceIdentityResult result) : ISurfaceHostIdentitySource
{
    /// <summary>Whether the source was consulted at all.</summary>
    public bool WasCalled { get; private set; }

    public ValueTask<HostSurfaceIdentityResult> AuthenticateAsync(
        HostSurfaceIdentityRequest request,
        CancellationToken cancellationToken = default)
    {
        WasCalled = true;
        return ValueTask.FromResult(result);
    }

    /// <summary>A source with nobody signed in.</summary>
    public static StubSurfaceHostIdentitySource Anonymous() =>
        new(HostSurfaceIdentityResult.Anonymous);
}
