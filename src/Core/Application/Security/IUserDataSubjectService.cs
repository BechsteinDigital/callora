using Callora.Core.Extensibility;

namespace Callora.Core.Application.Security;

/// <summary>
/// GDPR data-subject rights for backend users (PLAT-243): export of all
/// stored personal data (Art. 15) and erasure including audit-trail
/// anonymization (Art. 17).
/// </summary>
[CalloraInternal("GDPR data-subject aggregator — Core-owned, not a plugin contract (REV2 §7.2)")]
public interface IUserDataSubjectService
{
    /// <summary>All personal data stored for the user; null when unknown.</summary>
    Task<UserDataExport?> ExportAsync(string externalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the user and anonymizes audit entries referencing them.
    /// False when the user does not exist.
    /// </summary>
    Task<bool> EraseAsync(string externalId, CancellationToken cancellationToken = default);
}
