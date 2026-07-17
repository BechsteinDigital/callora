using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddPluginCapabilities : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
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

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "theme_assigned_at_utc",
            table: "workspaces",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "theme_assigned_by",
            table: "workspaces",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "theme_plugin_id",
            table: "workspaces",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "theme_version",
            table: "workspaces",
            type: "character varying(80)",
            maxLength: 80,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProvidedCapabilities",
            table: "plugin_installations",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RequiredCapabilities",
            table: "plugin_installations",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "workspace_template_definitions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                template_key = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                surface = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                plugin_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                display_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                template_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                parent_template_key = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                scope = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                priority = table.Column<int>(type: "integer", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_workspace_template_definitions", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "workspace_theme_setting_definitions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                setting_key = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                plugin_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                field_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                default_value_json = table.Column<string>(type: "text", nullable: true),
                is_required = table.Column<bool>(type: "boolean", nullable: false),
                sort_order = table.Column<int>(type: "integer", nullable: false),
                group_name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                options_json = table.Column<string>(type: "text", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_workspace_theme_setting_definitions", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "workspace_theme_setting_values",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                plugin_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                setting_key = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                value_json = table.Column<string>(type: "text", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_workspace_theme_setting_values", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_workspace_template_definitions_surface_is_active",
            table: "workspace_template_definitions",
            columns: new[] { "surface", "is_active" });

        migrationBuilder.CreateIndex(
            name: "IX_workspace_template_definitions_template_key",
            table: "workspace_template_definitions",
            column: "template_key");

        migrationBuilder.CreateIndex(
            name: "IX_workspace_template_definitions_template_key_surface_plugin_~",
            table: "workspace_template_definitions",
            columns: new[] { "template_key", "surface", "plugin_id", "version" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_workspace_theme_setting_definitions_plugin_id_version_is_ac~",
            table: "workspace_theme_setting_definitions",
            columns: new[] { "plugin_id", "version", "is_active" });

        migrationBuilder.CreateIndex(
            name: "IX_workspace_theme_setting_definitions_setting_key_plugin_id_v~",
            table: "workspace_theme_setting_definitions",
            columns: new[] { "setting_key", "plugin_id", "version" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_workspace_theme_setting_values_plugin_id_setting_key",
            table: "workspace_theme_setting_values",
            columns: new[] { "plugin_id", "setting_key" });

        migrationBuilder.CreateIndex(
            name: "IX_workspace_theme_setting_values_workspace_key_plugin_id_sett~",
            table: "workspace_theme_setting_values",
            columns: new[] { "workspace_key", "plugin_id", "setting_key" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "workspace_template_definitions");

        migrationBuilder.DropTable(
            name: "workspace_theme_setting_definitions");

        migrationBuilder.DropTable(
            name: "workspace_theme_setting_values");

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
            name: "theme_assigned_at_utc",
            table: "workspaces");

        migrationBuilder.DropColumn(
            name: "theme_assigned_by",
            table: "workspaces");

        migrationBuilder.DropColumn(
            name: "theme_plugin_id",
            table: "workspaces");

        migrationBuilder.DropColumn(
            name: "theme_version",
            table: "workspaces");

        migrationBuilder.DropColumn(
            name: "ProvidedCapabilities",
            table: "plugin_installations");

        migrationBuilder.DropColumn(
            name: "RequiredCapabilities",
            table: "plugin_installations");
    }
}
