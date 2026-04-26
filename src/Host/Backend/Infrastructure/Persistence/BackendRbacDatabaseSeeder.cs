using Callora.Host.Backend.Application.Policies;
using Callora.Host.Backend.Domain.Security;
using Callora.Host.Backend.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class BackendRbacDatabaseSeeder(
    BackendHostOptions options,
    IPasswordHasher<BackendUser> passwordHasher)
{
    public async Task SeedAsync(
        HostPersistenceDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var adminRole = await dbContext.BackendRbacRoles
            .Include(x => x.Permissions)
            .SingleOrDefaultAsync(x => x.Name == BackendRoles.Admin, cancellationToken)
            .ConfigureAwait(false);

        if (adminRole is null)
        {
            adminRole = new BackendRbacRole
            {
                Id = Guid.NewGuid(),
                Name = BackendRoles.Admin,
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

            dbContext.BackendRbacRoles.Add(adminRole);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            adminRole.IsSystem = true;
            adminRole.UpdatedAtUtc = DateTimeOffset.UtcNow;

            if (!adminRole.Permissions.Any(x => x.PermissionKey == "*"))
            {
                adminRole.Permissions.Add(new BackendRbacRoleGrant
                {
                    Id = Guid.NewGuid(),
                    RoleId = adminRole.Id,
                    PermissionKey = "*"
                });
            }
        }

        await EnsureDemoAdminUserAsync(dbContext, adminRole, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureDemoAdminUserAsync(
        HostPersistenceDbContext dbContext,
        BackendRbacRole adminRole,
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
                RoleId = adminRole.Id,
                AssignedAtUtc = nowUtc
            });
            return;
        }

        assignment.RoleId = adminRole.Id;
        assignment.AssignedAtUtc = nowUtc;
    }
}
