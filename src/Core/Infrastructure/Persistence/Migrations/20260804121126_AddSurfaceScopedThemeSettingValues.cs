using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSurfaceScopedThemeSettingValues : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_workspace_theme_setting_values_workspace_key_plugin_id_sett~",
            table: "workspace_theme_setting_values");

        migrationBuilder.AddColumn<string>(
            name: "surface_key",
            table: "workspace_theme_setting_values",
            type: "character varying(120)",
            maxLength: 120,
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateIndex(
            name: "IX_workspace_theme_setting_values_workspace_key_surface_key_pl~",
            table: "workspace_theme_setting_values",
            columns: new[] { "workspace_key", "surface_key", "plugin_id", "setting_key" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_workspace_theme_setting_values_workspace_key_surface_key_pl~",
            table: "workspace_theme_setting_values");

        migrationBuilder.DropColumn(
            name: "surface_key",
            table: "workspace_theme_setting_values");

        migrationBuilder.CreateIndex(
            name: "IX_workspace_theme_setting_values_workspace_key_plugin_id_sett~",
            table: "workspace_theme_setting_values",
            columns: new[] { "workspace_key", "plugin_id", "setting_key" },
            unique: true);
    }
}
