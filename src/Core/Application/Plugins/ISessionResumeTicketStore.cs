using Callora.Core.Domain.Plugins;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// Persistence for resume promises (ADR-018 §2.2). Host-internal: plugins reach this through
/// <see cref="Contracts.IHostSessionResumeService"/>, which binds every call to the calling plugin.
/// </summary>
public interface ISessionResumeTicketStore
{
    /// <summary>Stores one ticket.</summary>
    Task CreateAsync(SessionResumeTicketRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the ticket and returns what it removed, or null when there was nothing to remove.
    /// Single use has to be settled by the delete rather than by a read the caller could race.
    /// </summary>
    /// <param name="tokenHash">Hash of the presented secret.</param>
    /// <param name="pluginId">
    /// Plugin presenting the token. Part of the lookup rather than a check afterwards, so a foreign
    /// plugin cannot even learn that the ticket exists.
    /// </param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<SessionResumeTicketRecord?> ConsumeAsync(
        string tokenHash,
        string pluginId,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a ticket without returning it. Returns whether a row was removed.</summary>
    Task<bool> DeleteAsync(string tokenHash, string pluginId, CancellationToken cancellationToken = default);

    /// <summary>Removes every ticket past its expiry. Without this the table only ever grows.</summary>
    Task<int> PurgeExpiredAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
}
