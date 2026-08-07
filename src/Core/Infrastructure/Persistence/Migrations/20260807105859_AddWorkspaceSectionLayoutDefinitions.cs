using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddWorkspaceSectionLayoutDefinitions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Diese Migration legte die Tabelle nie an, sie fügte nur inherits_base hinzu —
        // auf eine Tabelle, die keine Migration erzeugt. Auf einer bestehenden
        // Entwicklungsdatenbank fiel das nicht auf, weil dort ein EnsureCreated-Lauf sie
        // hinterlassen hatte. Eine FRISCHE Installation brach beim Start ab:
        // 42P01, relation "workspace_section_layout_definitions" does not exist.
        migrationBuilder.CreateTable(
            name: "workspace_section_layout_definitions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                layout_key = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                plugin_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                regions_json = table.Column<string>(type: "text", nullable: false),
                sort_order = table.Column<int>(type: "integer", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                inherits_base = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_workspace_section_layout_definitions", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_workspace_section_layout_definitions_layout_key_plugin_id_v~",
            table: "workspace_section_layout_definitions",
            columns: ["layout_key", "plugin_id", "version"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_workspace_section_layout_definitions_plugin_id_version_is_a~",
            table: "workspace_section_layout_definitions",
            columns: ["plugin_id", "version", "is_active"]);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "workspace_section_layout_definitions");
    }
}
