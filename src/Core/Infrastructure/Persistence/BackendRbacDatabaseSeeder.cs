using Callora.Core.Application.Policies;
using Callora.Core.Domain.Security;
using Callora.Core.Infrastructure.Security;
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

        await EnsureDemoAdminUserAsync(dbContext, superAdminRole, cancellationToken).ConfigureAwait(false);
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

        var externalId = demoUser.ExternalId.Trim();
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

        user.Email = string.IsNullOrWhiteSpace(demoUser.Email) ? null : demoUser.Email.Trim();
        user.DisplayName = string.IsNullOrWhiteSpace(demoUser.DisplayName) ? null : demoUser.DisplayName.Trim();
        user.PasswordHash = passwordHasher.HashPassword(user, demoUser.Password);
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
