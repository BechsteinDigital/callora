using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddWorkspaceSurfaces : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "workspace_surfaces",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                surface_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                display_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                surface_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                public_base_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                public_host = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                public_path_prefix = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                access_mode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                locale = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                template_plugin_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                template_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                theme_plugin_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                theme_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                theme_assigned_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                theme_assigned_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_workspace_surfaces", x => x.Id);
                table.ForeignKey(
                    name: "FK_workspace_surfaces_workspaces_workspace_id",
                    column: x => x.workspace_id,
                    principalTable: "workspaces",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_workspace_surfaces_public_host",
            table: "workspace_surfaces",
            column: "public_host");

        migrationBuilder.CreateIndex(
            name: "IX_workspace_surfaces_workspace_id_surface_key",
            table: "workspace_surfaces",
            columns: new[] { "workspace_id", "surface_key" },
            unique: true);

        // Backfill: give every existing workspace a "default" surface that mirrors its
        // current public-routing and theme, so today's 1-workspace-1-access behaviour is
        // preserved. Surfaces are additive here; the runtime still reads the workspace
        // fields until the resolution baustein switches over (ADR-014 §14).
        migrationBuilder.Sql(
            """
            INSERT INTO workspace_surfaces (
                "Id", workspace_id, surface_key, display_name, surface_type,
                public_base_url, public_host, public_path_prefix, access_mode, locale,
                template_plugin_id, template_version,
                theme_plugin_id, theme_version, theme_assigned_by, theme_assigned_at_utc,
                is_active, created_at_utc, updated_at_utc)
            SELECT
                gen_random_uuid(), w."Id", 'default', w."DisplayName", 'spa',
                w.public_base_url, w.public_host, w.public_path_prefix, 'Mixed', NULL,
                NULL, NULL,
                w.theme_plugin_id, w.theme_version, w.theme_assigned_by, w.theme_assigned_at_utc,
                w."IsActive", now(), now()
            FROM workspaces w;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "workspace_surfaces");
    }
}
