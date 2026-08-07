using System.Collections.Concurrent;
using Callora.Plugin.Communication.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// Keeps the consumers that decide about inbound calls, per workspace, and offers each call around
/// until one takes it.
/// </summary>
internal sealed class IncomingCallOwnerRegistry : IIncomingCallOwnerRegistry
{
    private readonly ConcurrentDictionary<string, List<IIncomingCallOwner>> _owners =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger _logger;

    /// <summary>Creates the registry with an optional logger (defaults to a no-op).</summary>
    public IncomingCallOwnerRegistry(ILogger? logger = null) => _logger = logger ?? NullLogger.Instance;

    /// <inheritdoc />
    public IDisposable Register(string workspaceKey, IIncomingCallOwner owner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentNullException.ThrowIfNull(owner);

        var owners = _owners.GetOrAdd(workspaceKey, _ => []);
        lock (owners)
        {
            owners.Add(owner);
        }

        return new IncomingCallOwnerRegistration(this, workspaceKey, owner);
    }

    /// <summary>
    /// Whether anybody signed up for this workspace. Distinct from everybody declining: with no owners
    /// at all the inbound path keeps its previous behaviour, because a rule that refuses calls the
    /// moment it ships would refuse every one of them.
    /// </summary>
    public bool HasOwners(string workspaceKey)
    {
        if (!_owners.TryGetValue(workspaceKey, out var owners))
        {
            return false;
        }

        lock (owners)
        {
            return owners.Count > 0;
        }
    }

    /// <summary>
    /// Offers <paramref name="call"/> to the workspace's owners in registration order, returning who
    /// took it or <see langword="null"/> when nobody did.
    /// </summary>
    public async Task<CallOwnerIdentity?> OfferAsync(string workspaceKey, ICall call, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentNullException.ThrowIfNull(call);

        if (!_owners.TryGetValue(workspaceKey, out var owners))
        {
            return null;
        }

        IIncomingCallOwner[] snapshot;
        lock (owners)
        {
            snapshot = [.. owners];
        }

        return await new IncomingCallOwnership(snapshot, _logger)
            .OfferAsync(workspaceKey, call, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Removes one registration; called by the handle it handed out.</summary>
    internal void Unregister(string workspaceKey, IIncomingCallOwner owner)
    {
        if (!_owners.TryGetValue(workspaceKey, out var owners))
        {
            return;
        }

        lock (owners)
        {
            owners.Remove(owner);
        }
    }
}
