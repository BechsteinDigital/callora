using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;
using Callora.Core.Domain.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Callora.Core.Infrastructure.Persistence;

public sealed class BackendRbacDatabaseSeeder(
    BackendHostOptions options,
    IPasswordHasher<BackendUser> passwordHasher,
    ILogger<BackendRbacDatabaseSeeder> logger)
{
    // Seeded accounts are real super-admins. They pass the same
    // BackendPasswordPolicy as every later credential change (#104): below it the
    // account is refused, never seeded with a weaker password.

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

        // Bootstrap operator: seeded once on an empty install, never again.
        await EnsureInitialOperatorAsync(dbContext, superAdminRole, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

        // Persistent reminder for every start while the bootstrap operator stays on:
        // its credentials live in configuration/.env. After first sign-in, rotate the
        // password and set BackendHost__InitialOperator__Enabled=false, then remove
        // the credentials from .env.
        logger.LogWarning(
            "InitialOperator is enabled: bootstrap credentials are in configuration/.env. After first sign-in, change the password, set BackendHost__InitialOperator__Enabled=false, and remove the credentials from .env.");

        if (BackendPasswordPolicy.Validate(op.Password) is { } violation)
        {
            // Fail closed: a too-weak bootstrap password yields no operator (loud
            // warning) rather than a weak super-admin.
            logger.LogWarning(
                "InitialOperator was not seeded: {Violation} Set a stronger BackendHost__InitialOperator__Password.",
                violation);
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

        // Concurrent-start safety: the unique ExternalId/Email index makes a
        // duplicate operator impossible. If two fresh nodes race past the emptiness
        // check, one SaveChanges wins; the other fails its startup seed and restarts —
        // on restart the table is no longer empty, so it takes the skip path above.
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

        user.Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        user.DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        user.PasswordHashAlgorithm = "aspnet.identity.v3";
        // Re-seeding rewrites the credential, so every session issued under the old
        // one must die with it (#105).
        user.SecurityStamp = BackendSecurityStamp.New();

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
