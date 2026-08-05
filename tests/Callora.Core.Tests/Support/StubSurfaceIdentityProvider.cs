using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Scriptable <see cref="IHostSurfaceIdentityProvider"/>: answers with a fixed result,
/// throws, or stalls past the host's deadline — the three ways a real provider can
/// behave badly.
/// </summary>
public sealed class StubSurfaceIdentityProvider : IHostSurfaceIdentityProvider
{
    private readonly Func<HostSurfaceIdentityRequest, CancellationToken, Task<HostSurfaceIdentityResult>> _handler;

    /// <summary>Creates a provider with a scripted behaviour.</summary>
    /// <param name="pluginId">Plugin id the provider claims to belong to.</param>
    /// <param name="handler">What the provider does when invoked.</param>
    /// <param name="credentialSources">Credential sources the provider declares.</param>
    public StubSurfaceIdentityProvider(
        string pluginId,
        Func<HostSurfaceIdentityRequest, CancellationToken, Task<HostSurfaceIdentityResult>> handler,
        params SurfaceIdentityCredentialSource[] credentialSources)
    {
        PluginId = pluginId;
        _handler = handler;
        CredentialSources = credentialSources;
    }

    public string PluginId { get; }

    public IReadOnlyList<SurfaceIdentityCredentialSource> CredentialSources { get; }

    /// <summary>The request the provider last saw, for asserting what the host forwarded.</summary>
    public HostSurfaceIdentityRequest? LastRequest { get; private set; }

    public async ValueTask<HostSurfaceIdentityResult> AuthenticateAsync(
        HostSurfaceIdentityRequest request,
        CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return await _handler(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A provider that always returns the given result.</summary>
    /// <param name="pluginId">Plugin id the provider claims to belong to.</param>
    /// <param name="result">Result to return.</param>
    /// <param name="credentialSources">Credential sources the provider declares.</param>
    public static StubSurfaceIdentityProvider Returning(
        string pluginId,
        HostSurfaceIdentityResult result,
        params SurfaceIdentityCredentialSource[] credentialSources) =>
        new(pluginId, (_, _) => Task.FromResult(result), credentialSources);

    /// <summary>A provider that always throws.</summary>
    /// <param name="pluginId">Plugin id the provider claims to belong to.</param>
    public static StubSurfaceIdentityProvider Throwing(string pluginId) =>
        new(pluginId, (_, _) => throw new InvalidOperationException("provider exploded"));

    /// <summary>A provider that never answers before the host's deadline elapses.</summary>
    /// <param name="pluginId">Plugin id the provider claims to belong to.</param>
    public static StubSurfaceIdentityProvider Stalling(string pluginId) =>
        new(pluginId, async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
            return HostSurfaceIdentityResult.Anonymous;
        });
}
