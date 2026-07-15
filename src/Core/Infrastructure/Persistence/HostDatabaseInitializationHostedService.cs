using Callora.Core.Application.Policies;
using Callora.Core.Domain.Tenants;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

/// <summary>
/// One-time startup work: applies EF migrations under a Postgres advisory
/// lock (safe multi-instance start, PLAT-233), imports legacy filesystem
/// data-protection keys into the database keyring (PLAT-232) and seeds the
/// default tenant and RBAC baseline. Schema changes live exclusively in EF
/// migrations — the former inline DDL is gone.
/// </summary>
public sealed class HostDatabaseInitializationHostedService(IServiceProvider services) : IHostedService
{
    // Stabiler, callora-spezifischer Advisory-Lock-Schlüssel.
    private const long MigrationLockKey = 0x43414C4C4F5241;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HostPersistenceDbContext>();

        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ExecuteNonQueryAsync(connection, $"SELECT pg_advisory_lock({MigrationLockKey});", cancellationToken)
                .ConfigureAwait(false);
            try
            {
                await dbContext.ApplyMigrationsAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await ExecuteNonQueryAsync(connection, $"SELECT pg_advisory_unlock({MigrationLockKey});", cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            await connection.CloseAsync().ConfigureAwait(false);
        }

        var options = scope.ServiceProvider.GetRequiredService<BackendHostOptions>();
        await ImportLegacyDataProtectionKeysAsync(dbContext, options, cancellationToken).ConfigureAwait(false);
        await EnsureDefaultTenantExistsAsync(dbContext, options, cancellationToken).ConfigureAwait(false);

        var rbacSeeder = scope.ServiceProvider.GetRequiredService<BackendRbacDatabaseSeeder>();
        await rbacSeeder.SeedAsync(dbContext, cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Moves filesystem keyring entries into the database exactly once, so
    /// secrets protected before the keyring switch stay decryptable.
    /// </summary>
    private static async Task ImportLegacyDataProtectionKeysAsync(
        HostPersistenceDbContext dbContext,
        BackendHostOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.DataProtectionKeysPath) ||
            !Directory.Exists(options.DataProtectionKeysPath))
        {
            return;
        }

        var existingNames = await dbContext.DataProtectionKeys
            .AsNoTracking()
            .Select(x => x.FriendlyName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var known = new HashSet<string?>(existingNames, StringComparer.OrdinalIgnoreCase);

        var imported = 0;
        foreach (var keyFile in Directory.EnumerateFiles(options.DataProtectionKeysPath, "key-*.xml"))
        {
            var friendlyName = Path.GetFileNameWithoutExtension(keyFile);
            if (known.Contains(friendlyName))
            {
                continue;
            }

            dbContext.DataProtectionKeys.Add(new DataProtectionKey
            {
                FriendlyName = friendlyName,
                Xml = await File.ReadAllTextAsync(keyFile, cancellationToken).ConfigureAwait(false)
            });
            imported++;
        }

        if (imported > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task EnsureDefaultTenantExistsAsync(
        HostPersistenceDbContext dbContext,
        BackendHostOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.DefaultTenantKey))
        {
            return;
        }

        var tenantKey = options.DefaultTenantKey.Trim();
        var exists = await dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(x => x.TenantKey == tenantKey, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            return;
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var displayName = string.IsNullOrWhiteSpace(options.DefaultTenantDisplayName)
            ? "Default Tenant"
            : options.DefaultTenantDisplayName.Trim();

        dbContext.Tenants.Add(new Tenant
        {
            Id = Guid.NewGuid(),
            TenantKey = tenantKey,
            DisplayName = displayName,
            IsActive = true,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        });

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExecuteNonQueryAsync(
        System.Data.Common.DbConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
