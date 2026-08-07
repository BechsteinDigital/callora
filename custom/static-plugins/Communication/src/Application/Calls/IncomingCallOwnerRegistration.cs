using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// One consumer's sign-up for inbound calls, withdrawn on dispose. Idempotent, so a consumer that
/// tidies up twice does not remove a later registration of the same owner.
/// </summary>
internal sealed class IncomingCallOwnerRegistration : IDisposable
{
    private readonly IncomingCallOwnerRegistry _registry;
    private readonly string _workspaceKey;
    private readonly IIncomingCallOwner _owner;
    private bool _disposed;

    /// <summary>Creates the handle for one registration.</summary>
    public IncomingCallOwnerRegistration(
        IncomingCallOwnerRegistry registry, string workspaceKey, IIncomingCallOwner owner)
    {
        _registry = registry;
        _workspaceKey = workspaceKey;
        _owner = owner;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _registry.Unregister(_workspaceKey, _owner);
    }
}
