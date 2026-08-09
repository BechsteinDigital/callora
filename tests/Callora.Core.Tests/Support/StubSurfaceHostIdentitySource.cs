using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Surfaces;
using Callora.Core.Domain.Workspaces;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Host identity source returning a fixed result — stands in for "an operator is
/// already signed in at the backend" without an <c>HttpContext</c>.
/// </summary>
public sealed class StubSurfaceHostIdentitySource(HostSurfaceIdentityResult result) : ISurfaceHostIdentitySource
{
    /// <summary>Whether the source was consulted at all.</summary>
    public bool WasCalled { get; private set; }

    /// <summary>The authentication the source was consulted with, for assertions on ADR-023.</summary>
    public SurfaceAuthentication? SeenAuthentication { get; private set; }

    public ValueTask<HostSurfaceIdentityResult> AuthenticateAsync(
        HostSurfaceIdentityRequest request,
        SurfaceAuthentication authentication,
        CancellationToken cancellationToken = default)
    {
        WasCalled = true;
        SeenAuthentication = authentication;
        return ValueTask.FromResult(result);
    }

    /// <summary>A source with nobody signed in.</summary>
    public static StubSurfaceHostIdentitySource Anonymous() =>
        new(HostSurfaceIdentityResult.Anonymous);
}
