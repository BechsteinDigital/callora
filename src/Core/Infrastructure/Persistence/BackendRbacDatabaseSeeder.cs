using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;
using Callora.Core.Domain.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

public sealed class BackendRbacDatabaseSeeder(
    BackendHostOptions options,
    IPasswordHasher<BackendUser> passwordHasher)
{
    public async Task SeedAsync(
        HostPersistenceDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var superAdminRole = await dbContext.BackendRbacRoles
            .Include(x => x.Permissions)
            .SingleOrDefaultAsync(x => x.Name == BackendRoles.SuperAdmin, cancellationToken)
            .ConfigureAwait(false);

        if (superAdminRole is null)
        {
            superAdminRole = new BackendRbacRole
            {
                Id = Guid.NewGuid(),
                Name = BackendRoles.SuperAdmin,
                IsSystem = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Permissions =
                [
                    new BackendRbacRoleGrant
                    {
                        Id = Guid.NewGuid(),
                        PermissionKey = "*"
                    }
                ]
            };

            dbContext.BackendRbacRoles.Add(superAdminRole);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            superAdminRole.IsSystem = true;
            superAdminRole.UpdatedAtUtc = DateTimeOffset.UtcNow;

            if (!superAdminRole.Permissions.Any(x => x.PermissionKey == "*"))
            {
                superAdminRole.Permissions.Add(new BackendRbacRoleGrant
                {
                    Id = Guid.NewGuid(),
                    RoleId = superAdminRole.Id,
                    PermissionKey = "*"
                });
            }
        }

        // Demo admin: development convenience, re-seeded on every start when enabled.
        await EnsureDemoAdminUserAsync(dbContext, superAdminRole, cancellationToken).ConfigureAwait(false);
        // Initial operator: production bootstrap, seeded once on an empty install.
        await EnsureInitialOperatorAsync(dbContext, superAdminRole, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureDemoAdminUserAsync(
        HostPersistenceDbContext dbContext,
        BackendRbacRole superAdminRole,
        CancellationToken cancellationToken)
    {
        var demoUser = options.DemoAdminUser;
        if (demoUser is null ||
            !demoUser.Enabled ||
            string.IsNullOrWhiteSpace(demoUser.ExternalId) ||
            string.IsNullOrWhiteSpace(demoUser.Password))
        {
            return;
        }

        await UpsertOperatorAsync(
            dbContext, demoUser.ExternalId, demoUser.Email, demoUser.DisplayName, demoUser.Password, superAdminRole, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task EnsureInitialOperatorAsync(
        HostPersistenceDbContext dbContext,
        BackendRbacRole superAdminRole,
        CancellationToken cancellationToken)
    {
        var op = options.InitialOperator;
        if (op is null ||
            !op.Enabled ||
            string.IsNullOrWhiteSpace(op.ExternalId) ||
            string.IsNullOrWhiteSpace(op.Password))
        {
            return;
        }

        // Bootstrap only: never touch an install that already has users, so a
        // password changed later through the admin UI is never reset. Checks the
        // pending local context too, so a demo-admin seeded in the same run counts.
        var hasUsers = dbContext.BackendUsers.Local.Count > 0 ||
                       await dbContext.BackendUsers.AnyAsync(cancellationToken).ConfigureAwait(false);
        if (hasUsers)
        {
            return;
        }

        await UpsertOperatorAsync(
            dbContext, op.ExternalId, op.Email, op.DisplayName, op.Password, superAdminRole, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task UpsertOperatorAsync(
        HostPersistenceDbContext dbContext,
        string externalId,
        string? email,
        string? displayName,
        string password,
        BackendRbacRole superAdminRole,
        CancellationToken cancellationToken)
    {
        externalId = externalId.Trim();
        var user = await dbContext.BackendUsers
            .SingleOrDefaultAsync(x => x.ExternalId == externalId, cancellationToken)
            .ConfigureAwait(false);

        var nowUtc = DateTimeOffset.UtcNow;
        if (user is null)
        {
            user = new BackendUser
            {
                Id = Guid.NewGuid(),
                ExternalId = externalId,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            };
            dbContext.BackendUsers.Add(user);
        }
        else
        {
            user.UpdatedAtUtc = nowUtc;
        }

        user.Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        user.DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        user.PasswordHashAlgorithm = "aspnet.identity.v3";

        var assignment = await dbContext.BackendRbacUserRoles
            .SingleOrDefaultAsync(x => x.UserId == user.Id, cancellationToken)
            .ConfigureAwait(false);

        if (assignment is null)
        {
            dbContext.BackendRbacUserRoles.Add(new BackendRbacUserRole
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RoleId = superAdminRole.Id,
                AssignedAtUtc = nowUtc
            });
            return;
        }

        assignment.RoleId = superAdminRole.Id;
        assignment.AssignedAtUtc = nowUtc;
    }
}
