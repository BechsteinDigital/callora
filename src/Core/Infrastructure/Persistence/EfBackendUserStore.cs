using Callora.Core.Application.Security;
using Callora.Core.Domain.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

public sealed class EfBackendUserStore(
    HostPersistenceDbContext dbContext,
    IPasswordHasher<BackendUser> passwordHasher) : IBackendUserStore
{
    public async Task<BackendUser?> AuthenticateAsync(
        string login,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var normalizedLogin = login.Trim();
        var user = await dbContext.BackendUsers
            .SingleOrDefaultAsync(
                x => x.ExternalId == normalizedLogin || x.Email == normalizedLogin,
                cancellationToken)
            .ConfigureAwait(false);
        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return null;
        }

        // Disabled and locked-out accounts fail before the hash is even verified,
        // and produce the same null result as a wrong password — the caller must not
        // be able to distinguish the cases (#104).
        if (user.IsDisabled || IsLockedOut(user))
        {
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            await RecordFailedAttemptAsync(user, cancellationToken).ConfigureAwait(false);
            return null;
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            user.PasswordHashAlgorithm = "aspnet.identity.v3";
            user.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        await ClearFailedAttemptsAsync(user, cancellationToken).ConfigureAwait(false);
        return user;
    }

    public async Task<bool> SetEnabledAsync(
        string externalId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var user = await FindTrackedAsync(externalId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return false;
        }

        user.IsDisabled = !enabled;
        if (enabled)
        {
            // Re-enabling clears the guessing counters, so a lockout accumulated
            // before deactivation does not survive the reactivation.
            user.FailedAccessCount = 0;
            user.LockoutEndsAtUtc = null;
        }

        // Both directions revoke live sessions: disabling must stop them at once,
        // and re-enabling should not resurrect pre-deactivation tokens.
        user.SecurityStamp = BackendSecurityStamp.New();
        user.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> RevokeSessionsAsync(
        string externalId,
        CancellationToken cancellationToken = default)
    {
        var user = await FindTrackedAsync(externalId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return false;
        }

        user.SecurityStamp = BackendSecurityStamp.New();
        user.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static bool IsLockedOut(BackendUser user) =>
        user.LockoutEndsAtUtc is { } until && until > DateTimeOffset.UtcNow;

    private async Task RecordFailedAttemptAsync(BackendUser user, CancellationToken cancellationToken)
    {
        user.FailedAccessCount++;
        if (user.FailedAccessCount >= BackendLockoutPolicy.MaxFailedAttempts)
        {
            user.LockoutEndsAtUtc = DateTimeOffset.UtcNow.Add(BackendLockoutPolicy.LockoutDuration);
            user.FailedAccessCount = 0;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ClearFailedAttemptsAsync(BackendUser user, CancellationToken cancellationToken)
    {
        if (user.FailedAccessCount == 0 &&
            user.LockoutEndsAtUtc is null &&
            !dbContext.ChangeTracker.HasChanges())
        {
            return;
        }

        user.FailedAccessCount = 0;
        user.LockoutEndsAtUtc = null;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task<BackendUser?> FindTrackedAsync(string externalId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return Task.FromResult<BackendUser?>(null);
        }

        var normalizedExternalId = externalId.Trim();
        return dbContext.BackendUsers
            .SingleOrDefaultAsync(x => x.ExternalId == normalizedExternalId, cancellationToken);
    }

    public Task<bool> IsWorkspaceMemberAsync(
        string externalId,
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(workspaceKey))
        {
            return Task.FromResult(false);
        }

        var normalizedExternalId = externalId.Trim();
        var normalizedWorkspaceKey = workspaceKey.Trim();

        return dbContext.WorkspaceMemberships
            .AsNoTracking()
            .AnyAsync(
                x => x.User.ExternalId == normalizedExternalId &&
                     x.Workspace.WorkspaceKey == normalizedWorkspaceKey &&
                     x.Workspace.Tenant.IsActive &&
                     x.Workspace.IsActive,
                cancellationToken);
    }

    public Task<string?> GetWorkspaceRoleAsync(
        string externalId,
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(workspaceKey))
        {
            return Task.FromResult<string?>(null);
        }

        var normalizedExternalId = externalId.Trim();
        var normalizedWorkspaceKey = workspaceKey.Trim();

        return dbContext.WorkspaceMemberships
            .AsNoTracking()
            .Where(x => x.User.ExternalId == normalizedExternalId &&
                        x.Workspace.WorkspaceKey == normalizedWorkspaceKey &&
                        x.Workspace.Tenant.IsActive &&
                        x.Workspace.IsActive)
            .Select(x => x.Role)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<string?> GetTenantRoleAsync(
        string externalId,
        string tenantKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(tenantKey))
        {
            return Task.FromResult<string?>(null);
        }

        var normalizedExternalId = externalId.Trim();
        var normalizedTenantKey = tenantKey.Trim();

        // IsActive wird geprüft wie beim Workspace: Ein stillgelegter Mandant darf sich nicht
        // anmelden können, sonst wäre "deaktiviert" eine Anzeige und keine Grenze.
        return dbContext.TenantMemberships
            .AsNoTracking()
            .Where(x => x.User.ExternalId == normalizedExternalId &&
                        x.Tenant.TenantKey == normalizedTenantKey &&
                        x.Tenant.IsActive)
            .Select(x => x.Role)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BackendUser>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.BackendUsers
            .AsNoTracking()
            .OrderBy(x => x.ExternalId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BackendUser>> ListByWorkspaceAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return [];
        }

        var normalizedWorkspaceKey = workspaceKey.Trim();
        return await dbContext.WorkspaceMemberships
            .AsNoTracking()
            .Where(x => x.Workspace.WorkspaceKey == normalizedWorkspaceKey &&
                        x.Workspace.Tenant.IsActive &&
                        x.Workspace.IsActive)
            .Select(x => x.User)
            .OrderBy(x => x.ExternalId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<BackendUser?> GetByExternalIdAsync(
        string externalId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return Task.FromResult<BackendUser?>(null);
        }

        return dbContext.BackendUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.ExternalId == externalId.Trim(), cancellationToken);
    }

    public async Task<BackendUser> UpsertCredentialsAsync(
        string externalId,
        string? email,
        string? displayName,
        string? password,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        var normalizedExternalId = externalId.Trim();
        var normalizedEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        var normalizedDisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();

        var user = await dbContext.BackendUsers
            .SingleOrDefaultAsync(x => x.ExternalId == normalizedExternalId, cancellationToken)
            .ConfigureAwait(false);
        var nowUtc = DateTimeOffset.UtcNow;

        // One policy for onboarding and every later change (#104): a password is
        // validated here regardless of which caller supplied it.
        if (!string.IsNullOrWhiteSpace(password) &&
            BackendPasswordPolicy.Validate(password) is { } policyViolation)
        {
            throw new InvalidOperationException(policyViolation);
        }

        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw BackendUserException.PasswordRequired();
            }

            user = new BackendUser
            {
                Id = Guid.NewGuid(),
                ExternalId = normalizedExternalId,
                SecurityStamp = BackendSecurityStamp.New(),
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            };
            dbContext.BackendUsers.Add(user);
        }
        else
        {
            user.UpdatedAtUtc = nowUtc;
        }

        user.Email = normalizedEmail;
        user.DisplayName = normalizedDisplayName;

        if (!string.IsNullOrWhiteSpace(password))
        {
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            user.PasswordHashAlgorithm = "aspnet.identity.v3";
            // A credential change revokes every session issued with the old one (#105),
            // and clears the guessing counters for the new credential.
            user.SecurityStamp = BackendSecurityStamp.New();
            user.FailedAccessCount = 0;
            user.LockoutEndsAtUtc = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return user;
    }

    public async Task<bool> RemoveAsync(
        string externalId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return false;
        }

        var normalizedExternalId = externalId.Trim();
        var user = await dbContext.BackendUsers
            .SingleOrDefaultAsync(x => x.ExternalId == normalizedExternalId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return false;
        }

        dbContext.BackendUsers.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

}
