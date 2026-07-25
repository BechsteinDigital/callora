using Microsoft.EntityFrameworkCore;

namespace Callora.TestPlugin.DbContextPlugin;

/// <summary>
/// A plugin-owned EF Core context deriving from the host-provided
/// <see cref="DbContext"/>. Its whole purpose is to prove that the plugin ALC
/// resolves EF Core to the host's copy: if a duplicate EF Core were loaded from
/// the plugin directory, this type would derive from a different <c>DbContext</c>
/// identity and violate the <c>IPluginDbContextFactory&lt;TContext&gt;</c>
/// constraint at activation.
/// </summary>
public sealed class PluginTestDbContext : DbContext
{
    public PluginTestDbContext(DbContextOptions<PluginTestDbContext> options)
        : base(options)
    {
    }
}
