using Callora.Plugin.Communication.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// Offers an inbound call to the consumers that claim responsibility for calls, in order, until one
/// takes it.
/// </summary>
internal sealed class IncomingCallOwnership
{
    private readonly IReadOnlyList<IIncomingCallOwner> _owners;
    private readonly ILogger _logger;

    /// <summary>Creates the offer chain over the registered owners, in registration order.</summary>
    public IncomingCallOwnership(IReadOnlyList<IIncomingCallOwner> owners, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(owners);

        _owners = owners;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Offers <paramref name="call"/> to each owner until one claims it. Returns who took it, or
    /// <see langword="null"/> when nobody did — the caller decides what an unclaimed call deserves,
    /// because only it knows whether rejecting is better than letting it ring.
    /// </summary>
    public async Task<CallOwnerIdentity?> OfferAsync(string workspaceKey, ICall call, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentNullException.ThrowIfNull(call);

        foreach (var owner in _owners)
        {
            try
            {
                if (await owner.TryClaimAsync(workspaceKey, call, cancellationToken).ConfigureAwait(false))
                {
                    return owner.Identity;
                }
            }
            catch (Exception ex)
            {
                // One broken consumer must not make the trunk unreachable: a thrown claim counts as a
                // decline, and the call moves on to the next owner.
                _logger.LogWarning(ex,
                    "An incoming-call owner failed while being offered call {CallId}; treating it as declined.",
                    call.CallId);
            }
        }

        return null;
    }
}
