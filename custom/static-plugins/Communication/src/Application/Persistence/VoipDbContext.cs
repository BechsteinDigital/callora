using Microsoft.EntityFrameworkCore;

namespace Callora.Plugin.Communication.Application.Persistence;

/// <summary>
/// The voice plugin's own EF Core database (PLAT-260). All tables live in the
/// dedicated "plugin_communication" schema on the shared host database, so the plugin
/// owns its data with real entities, migrations and LINQ — and the host can
/// drop the schema cleanly on uninstall.
/// </summary>
public sealed class VoipDbContext(DbContextOptions<VoipDbContext> options) : DbContext(options)
{
    public const string SchemaName = "plugin_communication";

    public DbSet<CallLog> CallLogs => Set<CallLog>();

    public DbSet<SipAccount> SipAccounts => Set<SipAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);

        modelBuilder.Entity<CallLog>(entity =>
        {
            entity.ToTable("call_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.WorkspaceKey).HasMaxLength(120).IsRequired();
            entity.Property(x => x.CallId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ChannelId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Direction).HasMaxLength(20).IsRequired();
            entity.Property(x => x.TargetValue).HasMaxLength(200).IsRequired();
            entity.Property(x => x.TargetDisplayName).HasMaxLength(200);
            entity.HasIndex(x => new { x.WorkspaceKey, x.StartedAtUtc });
        });

        modelBuilder.Entity<SipAccount>(entity =>
        {
            entity.ToTable("sip_accounts");
            entity.HasKey(x => new { x.WorkspaceKey, x.SipAccountId });
            entity.Property(x => x.WorkspaceKey).HasMaxLength(120);
            entity.Property(x => x.SipAccountId).HasMaxLength(200);
            entity.Property(x => x.Username).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Domain).HasMaxLength(200).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ProtectedSecret).IsRequired();
        });
    }
}
