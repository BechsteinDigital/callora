using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <summary>
/// Renames the tenant-facing extension surface code from <c>workspace</c> to <c>surface</c>.
/// <para>
/// The old code named the wrong thing: a workspace is the container, a surface one of its access
/// points, and a workspace can expose several (ADR-014 §5). Only template definitions persist the
/// code — surface KEYS (<c>surface_key</c>, <c>surface_type</c>) name a concrete surface such as
/// "portal" and are untouched.
/// </para>
/// <para>
/// Data-only: no schema changes, hence no model snapshot change. The down migration restores the
/// old code so a rollback leaves a consistent database, even though the code no longer reads it.
/// </para>
/// </summary>
public partial class RenameWorkspaceSurfaceCodeToSurface : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "UPDATE workspace_template_definitions SET surface = 'surface' WHERE surface = 'workspace';");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "UPDATE workspace_template_definitions SET surface = 'workspace' WHERE surface = 'surface';");
    }
}
