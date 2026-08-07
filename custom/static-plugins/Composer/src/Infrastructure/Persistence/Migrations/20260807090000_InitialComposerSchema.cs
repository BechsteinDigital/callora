using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Plugin.Composer.Infrastructure.Persistence.Migrations;

/// <summary>
/// The Composer's own schema: layouts and their versions.
/// <para>
/// Written by hand rather than scaffolded — the EF tooling cannot build a plugin DbContext
/// (Callora.Core is not in the plugin's output), so <c>dotnet ef migrations add</c> has nothing to
/// load. The Communication plugin's migrations are written the same way.
/// </para>
/// </summary>
public partial class InitialComposerSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "plugin_composer");

        migrationBuilder.CreateTable(
            name: "surface_layouts",
            schema: "plugin_composer",
            columns: table => new
            {
                key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                workspace_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                surface_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_surface_layouts", x => x.key));

        migrationBuilder.CreateTable(
            name: "surface_layout_versions",
            schema: "plugin_composer",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                layout_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                version_number = table.Column<int>(type: "integer", nullable: false),
                state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                // jsonb, nicht text: das Dokument wird als Ganzes gespeichert, aber „welche Layouts
                // benutzen Block X" muss beantwortbar bleiben, ohne jede Zeile zu deserialisieren.
                document = table.Column<string>(type: "jsonb", nullable: false),
                label = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                changed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                published_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_surface_layout_versions", x => x.id));

        // Der Renderpfad fragt bei JEDEM Aufruf, welches Layout zu dieser Fläche gehört.
        migrationBuilder.CreateIndex(
            name: "IX_surface_layouts_workspace_key_surface_key",
            schema: "plugin_composer",
            table: "surface_layouts",
            columns: ["workspace_key", "surface_key"]);

        migrationBuilder.CreateIndex(
            name: "IX_surface_layout_versions_layout_key_state",
            schema: "plugin_composer",
            table: "surface_layout_versions",
            columns: ["layout_key", "state"]);

        // Eine Versionsnummer je Layout genau einmal — die Nummer IST die Identität einer
        // Veröffentlichung, und zwei gleiche machten eine Historie unlesbar.
        migrationBuilder.CreateIndex(
            name: "IX_surface_layout_versions_layout_key_version_number",
            schema: "plugin_composer",
            table: "surface_layout_versions",
            columns: ["layout_key", "version_number"],
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "surface_layout_versions", schema: "plugin_composer");
        migrationBuilder.DropTable(name: "surface_layouts", schema: "plugin_composer");
    }
}
