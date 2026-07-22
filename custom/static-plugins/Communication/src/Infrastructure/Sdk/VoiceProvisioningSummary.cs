namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// Outcome of a <see cref="VoiceChannelProvisioner.ProvisionAsync"/> run: how many enabled accounts
/// were offered and how many became live, registered channels.
/// </summary>
/// <param name="TotalAccounts">Enabled accounts offered for provisioning.</param>
/// <param name="ConnectedChannels">Accounts that connected and were registered as channels.</param>
public sealed record VoiceProvisioningSummary(int TotalAccounts, int ConnectedChannels);
