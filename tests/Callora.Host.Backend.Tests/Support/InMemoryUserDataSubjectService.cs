using Callora.Host.Backend.Application.Abstractions.Security;

namespace Callora.Host.Backend.Tests.Support;

/// <summary>
/// Test double for data-subject rights: export from and erasure via the
/// in-memory user store, without audit anonymization.
/// </summary>
public sealed class InMemoryUserDataSubjectService(IBackendUserStore userStore) : IUserDataSubjectService
{
    public async Task<UserDataExport?> ExportAsync(string externalId, CancellationToken cancellationToken = default)
    {
        var user = await userStore.GetByExternalIdAsync(externalId, cancellationToken);
        return user is null
            ? null
            : new UserDataExport(
                user.ExternalId,
                user.Email,
                user.DisplayName,
                user.CreatedAtUtc,
                Role: null,
                Memberships: [],
                AuditTrail: []);
    }

    public Task<bool> EraseAsync(string externalId, CancellationToken cancellationToken = default) =>
        userStore.RemoveAsync(externalId, cancellationToken);
}
