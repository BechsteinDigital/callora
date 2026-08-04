using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <summary>
/// Moves public routing and the access mode off the workspace: a workspace is the
/// data container, every way into it is a surface (ADR-014 §5). The columns were
/// duplicated onto the workspace's "default" surface since AddWorkspaceSurfaces;
/// this migration makes sure that copy is complete and then drops the originals.
/// </summary>
public partial class MoveWorkspaceRouteToSurfaces : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. A workspace without a default surface gets one carrying its route.
        //    AddWorkspaceSurfaces backfilled these, so this only catches rows
        //    created by a path that bypassed it.
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
                w.public_base_url, w.public_host, w.public_path_prefix,
                CASE WHEN w.surface_access_policy = 'Authenticated' THEN 'Authenticated' ELSE 'Mixed' END,
                NULL, NULL, NULL,
                w.theme_plugin_id, w.theme_version, w.theme_assigned_by, w.theme_assigned_at_utc,
                w."IsActive", now(), now()
            FROM workspaces w
            WHERE NOT EXISTS (
                SELECT 1 FROM workspace_surfaces s
                WHERE s.workspace_id = w."Id" AND s.surface_key = 'default');
            """);

        // 2. An existing default surface that never received the route takes it now.
        migrationBuilder.Sql(
            """
            UPDATE workspace_surfaces s
            SET public_base_url = w.public_base_url,
                public_host = w.public_host,
                public_path_prefix = w.public_path_prefix,
                updated_at_utc = now()
            FROM workspaces w
            WHERE s.workspace_id = w."Id"
              AND s.surface_key = 'default'
              AND s.public_host IS NULL
              AND s.public_base_url IS NULL
              AND (w.public_host IS NOT NULL OR w.public_base_url IS NOT NULL);
            """);

        // 3. Carry the workspace-wide access policy over. Without this a workspace
        //    that required authentication would silently become reachable through
        //    a 'Mixed' default surface after the drop.
        migrationBuilder.Sql(
            """
            UPDATE workspace_surfaces s
            SET access_mode = 'Authenticated',
                updated_at_utc = now()
            FROM workspaces w
            WHERE s.workspace_id = w."Id"
              AND s.surface_key = 'default'
              AND w.surface_access_policy = 'Authenticated'
              AND s.access_mode <> 'Authenticated';
            """);

        migrationBuilder.DropColumn(
            name: "public_base_url",
            table: "workspaces");

        migrationBuilder.DropColumn(
            name: "public_host",
            table: "workspaces");

        migrationBuilder.DropColumn(
            name: "public_path_prefix",
            table: "workspaces");

        migrationBuilder.DropColumn(
            name: "surface_access_policy",
            table: "workspaces");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "public_base_url",
            table: "workspaces",
            type: "character varying(2048)",
            maxLength: 2048,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "public_host",
            table: "workspaces",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "public_path_prefix",
            table: "workspaces",
            type: "character varying(500)",
            maxLength: 500,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "surface_access_policy",
            table: "workspaces",
            type: "character varying(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "Public");

        // Restore the workspace columns from its default surface, so a rollback
        // does not lose the routing it just gave up.
        migrationBuilder.Sql(
            """
            UPDATE workspaces w
            SET public_base_url = s.public_base_url,
                public_host = s.public_host,
                public_path_prefix = COALESCE(s.public_path_prefix, '/'),
                surface_access_policy =
                    CASE WHEN s.access_mode = 'Authenticated' THEN 'Authenticated' ELSE 'Public' END
            FROM workspace_surfaces s
            WHERE s.workspace_id = w."Id" AND s.surface_key = 'default';
            """);
    }
}
