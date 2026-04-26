using Callora.Host.Backend.Application.Policies;
using Callora.Host.Backend.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class HostDatabaseInitializationHostedService(IServiceProvider services) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HostPersistenceDbContext>();
        await dbContext.MigrateOrEnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        await EnsureWorkspaceTemplateTablesAsync(dbContext, cancellationToken).ConfigureAwait(false);

        var options = scope.ServiceProvider.GetRequiredService<BackendHostOptions>();
        await EnsureDefaultTenantExistsAsync(dbContext, options, cancellationToken).ConfigureAwait(false);

        var rbacSeeder = scope.ServiceProvider.GetRequiredService<BackendRbacDatabaseSeeder>();
        await rbacSeeder.SeedAsync(dbContext, cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task EnsureDefaultTenantExistsAsync(
        HostPersistenceDbContext dbContext,
        BackendHostOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.DefaultTenantKey))
        {
            return;
        }

        if (!await HasTenantsTableAsync(dbContext, cancellationToken).ConfigureAwait(false))
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

    private static async Task<bool> HasTenantsTableAsync(
        HostPersistenceDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var wasClosed = connection.State == System.Data.ConnectionState.Closed;
        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT to_regclass('public.tenants') IS NOT NULL";
            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return result is bool exists && exists;
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task EnsureWorkspaceTemplateTablesAsync(
        HostPersistenceDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var wasClosed = connection.State == System.Data.ConnectionState.Closed;
        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await ExecuteNonQueryAsync(connection, """
                CREATE TABLE IF NOT EXISTS workspace_template_definitions (
                    id uuid PRIMARY KEY,
                    template_key character varying(180) NOT NULL,
                    surface character varying(40) NOT NULL,
                    plugin_id character varying(200) NOT NULL,
                    version character varying(80) NOT NULL,
                    display_name character varying(300) NOT NULL,
                    template_path character varying(1000) NOT NULL,
                    parent_template_key character varying(180) NULL,
                    scope character varying(40) NOT NULL,
                    is_active boolean NOT NULL,
                    priority integer NOT NULL,
                    created_at_utc timestamp with time zone NOT NULL,
                    updated_at_utc timestamp with time zone NOT NULL
                );
                """, cancellationToken).ConfigureAwait(false);

            await ExecuteNonQueryAsync(connection, """
                CREATE UNIQUE INDEX IF NOT EXISTS ix_workspace_template_definitions_key_surface_plugin_version
                ON workspace_template_definitions (template_key, surface, plugin_id, version);
                """, cancellationToken).ConfigureAwait(false);

            await ExecuteNonQueryAsync(connection, """
                CREATE INDEX IF NOT EXISTS ix_workspace_template_definitions_surface_active
                ON workspace_template_definitions (surface, is_active);
                """, cancellationToken).ConfigureAwait(false);

            await ExecuteNonQueryAsync(connection, """
                CREATE INDEX IF NOT EXISTS ix_workspace_template_definitions_template_key
                ON workspace_template_definitions (template_key);
                """, cancellationToken).ConfigureAwait(false);

            await ExecuteNonQueryAsync(connection, """
                ALTER TABLE workspace_template_definitions
                ADD COLUMN IF NOT EXISTS parent_template_key character varying(180) NULL;
                """, cancellationToken).ConfigureAwait(false);

            await ExecuteNonQueryAsync(connection, """
                ALTER TABLE workspaces
                ADD COLUMN IF NOT EXISTS public_base_url character varying(2048) NULL;
                """, cancellationToken).ConfigureAwait(false);

            await ExecuteNonQueryAsync(connection, """
                ALTER TABLE workspaces
                ADD COLUMN IF NOT EXISTS public_host character varying(500) NULL;
                """, cancellationToken).ConfigureAwait(false);

            await ExecuteNonQueryAsync(connection, """
                ALTER TABLE workspaces
                ADD COLUMN IF NOT EXISTS public_path_prefix character varying(500) NOT NULL DEFAULT '/';
                """, cancellationToken).ConfigureAwait(false);

            await ExecuteNonQueryAsync(connection, """
                UPDATE workspaces SET public_path_prefix = '/' WHERE public_path_prefix IS NULL OR public_path_prefix = '';
                """, cancellationToken).ConfigureAwait(false);

            await ExecuteNonQueryAsync(connection, """
                ALTER TABLE workspaces
                ADD COLUMN IF NOT EXISTS theme_plugin_id character varying(200) NULL;
                """, cancellationToken).ConfigureAwait(false);

            await ExecuteNonQueryAsync(connection, """
                ALTER TABLE workspaces
                ADD COLUMN IF NOT EXISTS theme_version character varying(80) NULL;
                """, cancellationToken).ConfigureAwait(false);

            await ExecuteNonQueryAsync(connection, """
                ALTER TABLE workspaces
                ADD COLUMN IF NOT EXISTS theme_assigned_by character varying(200) NULL;
                """, cancellationToken).ConfigureAwait(false);

            await ExecuteNonQueryAsync(connection, """
                ALTER TABLE workspaces
                ADD COLUMN IF NOT EXISTS theme_assigned_at_utc timestamp with time zone NULL;
                """, cancellationToken).ConfigureAwait(false);

            await ExecuteNonQueryAsync(connection, """
                CREATE TABLE IF NOT EXISTS workspace_theme_setting_definitions (
                    id uuid PRIMARY KEY,
                    setting_key character varying(180) NOT NULL,
                    plugin_id character varying(200) NOT NULL,
                    version character varying(80) NOT NULL,
                    label character varying(300) NOT NULL,
                    field_type character varying(80) NOT NULL,
                    description character varying(2000) NULL,
                    default_value_json text NULL,
                    is_required boolean NOT NULL,
                    sort_order integer NOT NULL,
                    group_name character varying(180) NULL,
                    options_json text NULL,
                    is_active boolean NOT NULL,
                    created_at_utc timestamp with time zone NOT NULL,
                    updated_at_utc timestamp with time zone NOT NULL
                );
                """, cancellationToken).ConfigureAwait(false);

            await ExecuteNonQueryAsync(connection, """
                CREATE UNIQUE INDEX IF NOT EXISTS ix_workspace_theme_setting_definitions_key_plugin_version
                ON workspace_theme_setting_definitions (setting_key, plugin_id, version);
                """, cancellationToken).ConfigureAwait(false);

            await ExecuteNonQueryAsync(connection, """
                CREATE INDEX IF NOT EXISTS ix_workspace_theme_setting_definitions_plugin_version_active
                ON workspace_theme_setting_definitions (plugin_id, version, is_active);
                """, cancellationToken).ConfigureAwait(false);

            await ExecuteNonQueryAsync(connection, """
                CREATE TABLE IF NOT EXISTS workspace_theme_setting_values (
                    id uuid PRIMARY KEY,
                    workspace_key character varying(120) NOT NULL,
                    plugin_id character varying(200) NOT NULL,
                    setting_key character varying(180) NOT NULL,
                    value_json text NOT NULL,
                    updated_at_utc timestamp with time zone NOT NULL
                );
                """, cancellationToken).ConfigureAwait(false);

            await ExecuteNonQueryAsync(connection, """
                CREATE UNIQUE INDEX IF NOT EXISTS ix_workspace_theme_setting_values_workspace_plugin_key
                ON workspace_theme_setting_values (workspace_key, plugin_id, setting_key);
                """, cancellationToken).ConfigureAwait(false);

            await ExecuteNonQueryAsync(connection, """
                CREATE INDEX IF NOT EXISTS ix_workspace_theme_setting_values_plugin_key
                ON workspace_theme_setting_values (plugin_id, setting_key);
                """, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
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
