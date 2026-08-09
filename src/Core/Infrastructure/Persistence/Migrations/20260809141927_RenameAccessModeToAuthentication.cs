using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <summary>
/// Turns the access mode into the authentication axis (ADR-023).
/// <para>
/// The value mapping changes no behaviour: `Mixed` served anonymously exactly like `Public`,
/// and `Authenticated` already branched on whether an identity plugin was assigned — to the
/// plugin's 401 with one, to the operator login without. Those two cases are what get names
/// here. What was implicit in the host becomes a stored, editable choice.
/// </para>
/// <para>
/// The column stores the enum NAME, not its number (value converter). So the data has to be
/// rewritten, and it has to happen while the old names are still in the column.
/// </para>
/// </summary>
public partial class RenameAccessModeToAuthentication : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Before the rename: the old names are still what is in there.
        migrationBuilder.Sql(
            """
            UPDATE workspace_surfaces
               SET access_mode = CASE
                   WHEN access_mode = 'Authenticated'
                        AND identity_plugin_id IS NOT NULL
                        AND btrim(identity_plugin_id) <> '' THEN 'SurfaceIdentity'
                   WHEN access_mode = 'Authenticated' THEN 'Administration'
                   ELSE 'Public'
               END;
            """);

        migrationBuilder.RenameColumn(
            name: "access_mode",
            table: "workspace_surfaces",
            newName: "authentication");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "authentication",
            table: "workspace_surfaces",
            newName: "access_mode");

        // Lossy on purpose in one direction: `Mixed` cannot be recovered, because nothing
        // distinguished it from `Public` while it existed. Everything else round-trips.
        migrationBuilder.Sql(
            """
            UPDATE workspace_surfaces
               SET access_mode = CASE
                   WHEN access_mode IN ('SurfaceIdentity', 'Administration') THEN 'Authenticated'
                   ELSE 'Public'
               END;
            """);
    }
}
