using Callora.Plugin.Communication.Domain.Accounts;
using Callora.Plugin.Communication.Domain.Calls;
using Callora.Plugin.Communication.Domain.Lines;
using Microsoft.EntityFrameworkCore;

namespace Callora.Plugin.Communication.Infrastructure.Persistence;

/// <summary>
/// The Communication plugin's own EF Core database (PLAT-260). All tables live in the
/// dedicated <c>plugin_communication</c> schema on the shared host database, so the plugin
/// owns its data with real entities, migrations and LINQ — and the host can drop the schema
/// cleanly on uninstall.
/// </summary>
public sealed class CommunicationDbContext(DbContextOptions<CommunicationDbContext> options)
    : DbContext(options)
{
    /// <summary>Dedicated Postgres schema for this plugin.</summary>
    public const string SchemaName = "plugin_communication";

    /// <summary>Configured SIP accounts.</summary>
    public DbSet<SipAccount> SipAccounts => Set<SipAccount>();

    /// <summary>Callable lines under accounts.</summary>
    public DbSet<SipLine> SipLines => Set<SipLine>();

    /// <summary>Call history (metadata only).</summary>
    public DbSet<CallLog> CallLogs => Set<CallLog>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommunicationDbContext).Assembly);
    }
}
