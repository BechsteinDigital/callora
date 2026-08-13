using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;
using Callora.Core.Domain.Security;
using Callora.Core.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Callora.Core.Tests.Infrastructure.Persistence;

/// <summary>
/// The initial-operator bootstrap seeds exactly one super admin on a fresh install
/// and never touches an install that already has users. Runs against a real
/// Postgres; skipped automatically when Docker is unavailable.
/// </summary>
[Trait("Category", "Slow")]
public sealed class BackendRbacDatabaseSeederTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private bool _started;

    public async Task InitializeAsync()
    {
        try
        {
            await _postgres.StartAsync();
            _started = true;
        }
        catch (Exception)
        {
            _started = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_started)
        {
            await _postgres.DisposeAsync();
        }
    }

    private static BackendRbacDatabaseSeeder Seeder(BackendHostOptions options) =>
        new(options, new PasswordHasher<BackendUser>(), NullLogger<BackendRbacDatabaseSeeder>.Instance);

    // A fresh, isolated database per test — creating a new one avoids dropping the
    // container's currently-open database (Postgres 55006) and keeps tests
    // order-independent.
    private async Task<DbContextOptions<HostPersistenceDbContext>> FreshDbAsync()
    {
        var dbName = "seed_" + Guid.NewGuid().ToString("N");
        await using (var admin = new NpgsqlConnection(_postgres.GetConnectionString()))
        {
            await admin.OpenAsync();
            await using var cmd = admin.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE \"{dbName}\"";
            await cmd.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Database = dbName,
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var ctx = new HostPersistenceDbContext(options);
        await ctx.Database.EnsureCreatedAsync();
        return options;
    }

    [SkippableFact]
    public async Task InitialOperator_SeedsSuperAdminUser_OnEmptyInstall()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");
        var options = await FreshDbAsync();

        var hostOptions = new BackendHostOptions
        {
            InitialOperator = { Enabled = true, ExternalId = "root", Email = "root@x.io", Password = "s3cret-pw-1234" },
        };

        await using (var ctx = new HostPersistenceDbContext(options))
        {
            await Seeder(hostOptions).SeedAsync(ctx);
        }

        await using var verify = new HostPersistenceDbContext(options);
        var user = await verify.BackendUsers.SingleOrDefaultAsync(x => x.ExternalId == "root");
        Assert.NotNull(user);
        Assert.Equal("root@x.io", user!.Email);
        Assert.False(string.IsNullOrWhiteSpace(user.PasswordHash));

        var role = await verify.BackendRbacRoles.SingleAsync(x => x.Name == BackendRoles.SuperAdmin);
        var assignment = await verify.BackendRbacUserRoles.SingleAsync(x => x.UserId == user.Id);
        Assert.Equal(role.Id, assignment.RoleId);
    }

    /// <summary>
    /// Der Grund, aus dem der re-seedende Demo-Admin verschwunden ist: Ein im Admin geändertes
    /// Passwort muss den nächsten Start überleben.
    /// <para>
    /// Vorher setzte <c>EnsureDemoAdminUserAsync</c> den Zugang bei JEDEM Start neu. Wer sein
    /// Passwort änderte, hatte es nach dem nächsten Deployment still wieder verloren — ohne
    /// Fehler, ohne Log, und die Hygieneprüfung schwieg dazu, sobald ein eigenes Passwort
    /// konfiguriert war.
    /// </para>
    /// </summary>
    [SkippableFact]
    public async Task AChangedPasswordSurvivesTheNextSeedRun()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");
        var options = await FreshDbAsync();

        var hostOptions = new BackendHostOptions
        {
            InitialOperator = { Enabled = true, ExternalId = "root", Password = "s3cret-pw-1234" },
        };

        await using (var ctx = new HostPersistenceDbContext(options))
        {
            await Seeder(hostOptions).SeedAsync(ctx);
        }

        // Der Betreiber ändert sein Passwort über die Oberfläche.
        string changedHash;
        await using (var change = new HostPersistenceDbContext(options))
        {
            var user = await change.BackendUsers.SingleAsync(x => x.ExternalId == "root");
            user.PasswordHash = "hash-set-through-the-admin-ui";
            await change.SaveChangesAsync();
            changedHash = user.PasswordHash;
        }

        // Nächster Start, gleiche Konfiguration.
        await using (var restart = new HostPersistenceDbContext(options))
        {
            await Seeder(hostOptions).SeedAsync(restart);
        }

        await using var verify = new HostPersistenceDbContext(options);
        var after = await verify.BackendUsers.SingleAsync(x => x.ExternalId == "root");
        Assert.Equal(changedHash, after.PasswordHash);
    }

    [SkippableFact]
    public async Task InitialOperator_DoesNotSeed_WhenAUserAlreadyExists()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");
        var options = await FreshDbAsync();

        await using (var pre = new HostPersistenceDbContext(options))
        {
            pre.BackendUsers.Add(new BackendUser
            {
                Id = Guid.NewGuid(),
                ExternalId = "existing",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
            await pre.SaveChangesAsync();
        }

        var hostOptions = new BackendHostOptions
        {
            InitialOperator = { Enabled = true, ExternalId = "root", Password = "s3cret-pw-1234" },
        };

        await using (var ctx = new HostPersistenceDbContext(options))
        {
            await Seeder(hostOptions).SeedAsync(ctx);
        }

        await using var verify = new HostPersistenceDbContext(options);
        Assert.False(await verify.BackendUsers.AnyAsync(x => x.ExternalId == "root"));
    }

    [SkippableFact]
    public async Task InitialOperator_SeedsUser_WhenPasswordExactlyMinimumLength()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");
        var options = await FreshDbAsync();

        var hostOptions = new BackendHostOptions
        {
            // Exactly 12 characters — the boundary is allowed (guards a <= off-by-one).
            InitialOperator = { Enabled = true, ExternalId = "root", Password = "twelvechars!" },
        };

        await using (var ctx = new HostPersistenceDbContext(options))
        {
            await Seeder(hostOptions).SeedAsync(ctx);
        }

        await using var verify = new HostPersistenceDbContext(options);
        Assert.True(await verify.BackendUsers.AnyAsync(x => x.ExternalId == "root"));
    }

    [SkippableFact]
    public async Task InitialOperator_Disabled_SeedsNoUser()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");
        var options = await FreshDbAsync();

        var hostOptions = new BackendHostOptions
        {
            InitialOperator = { Enabled = false, ExternalId = "root", Password = "s3cret-pw-1234" },
        };

        await using (var ctx = new HostPersistenceDbContext(options))
        {
            await Seeder(hostOptions).SeedAsync(ctx);
        }

        await using var verify = new HostPersistenceDbContext(options);
        Assert.False(await verify.BackendUsers.AnyAsync());
    }

    [SkippableFact]
    public async Task InitialOperator_SeedsNoUser_WhenPasswordBelowMinimumLength()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");
        var options = await FreshDbAsync();

        var hostOptions = new BackendHostOptions
        {
            // 8 characters — below the 12-character minimum: refused, not weakened.
            InitialOperator = { Enabled = true, ExternalId = "root", Password = "short-pw" },
        };

        await using (var ctx = new HostPersistenceDbContext(options))
        {
            await Seeder(hostOptions).SeedAsync(ctx);
        }

        await using var verify = new HostPersistenceDbContext(options);
        Assert.False(await verify.BackendUsers.AnyAsync());
    }
}
