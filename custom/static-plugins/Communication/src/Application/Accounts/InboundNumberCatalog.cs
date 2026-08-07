using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Accounts;

/// <summary>
/// Reads the workspace's inbound numbers off its configured accounts.
/// </summary>
/// <remarks>
/// Over the store rather than the live channel registry on purpose: a number belongs to the contract
/// with the carrier, not to whether the trunk happens to be registered this minute. Assigning a number
/// before switching the line on is the normal order of work, so a disabled account still offers its
/// numbers.
/// </remarks>
public sealed class InboundNumberCatalog(ISipAccountStore accounts) : IInboundNumberCatalog
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<InboundNumber>> ListAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);

        var configured = await accounts.ListAsync(workspaceKey, cancellationToken).ConfigureAwait(false);

        // Not deduplicated: the same number on two trunks is broken configuration, and hiding one of
        // the two would leave an operator wondering why calls land on the wrong line.
        return
        [
            .. configured.SelectMany(account => account.Connection.InboundNumbers
                .Select(number => new InboundNumber(number, account.Id, account.DisplayName)))
        ];
    }
}
