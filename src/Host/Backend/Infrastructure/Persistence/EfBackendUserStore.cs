using Callora.Host.Backend.Application.Security;
using Callora.Host.Backend.Domain.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Persistence;

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

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return verification switch
        {
            PasswordVerificationResult.Success => user,
            PasswordVerificationResult.SuccessRehashNeeded => await RehashOnLoginAsync(user, password, cancellationToken).ConfigureAwait(false),
            _ => null
        };
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

        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("Password is required when creating a new user.");
            }

            user = new BackendUser
            {
                Id = Guid.NewGuid(),
                ExternalId = normalizedExternalId,
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

    private async Task<BackendUser?> RehashOnLoginAsync(
        BackendUser user,
        string password,
        CancellationToken cancellationToken)
    {
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        user.PasswordHashAlgorithm = "aspnet.identity.v3";
        user.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return user;
    }
}
