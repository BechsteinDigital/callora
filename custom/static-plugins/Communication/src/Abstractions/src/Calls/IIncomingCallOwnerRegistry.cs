namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Where a consumer signs up to decide about inbound calls.
/// </summary>
/// <remarks>
/// <para>A registry rather than an export, for the same reason channels use one: several consumers may
/// own calls at once, and the host resolves a contract to a single provider — multi-provider exports
/// are collected by the host and deliberately not reachable through a consuming plugin's service
/// surface. Signing up here keeps the list in the hands of the plugin that offers the calls.</para>
/// <para>Owners are offered a call in registration order, and the first one that claims it gets it.
/// A consumer responsible for particular numbers checks
/// <see cref="ICall.InboundIdentity"/> and declines the rest.</para>
/// </remarks>
public interface IIncomingCallOwnerRegistry
{
    /// <summary>
    /// Signs <paramref name="owner"/> up for inbound calls in <paramref name="workspaceKey"/>. Dispose
    /// the registration to stop receiving them — a consumer being deactivated must take its claim with
    /// it, or calls would be offered to something that is no longer there.
    /// </summary>
    IDisposable Register(string workspaceKey, IIncomingCallOwner owner);
}
