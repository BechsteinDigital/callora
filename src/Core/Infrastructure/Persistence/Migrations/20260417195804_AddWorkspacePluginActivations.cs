using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddWorkspacePluginActivations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "workspace_plugin_activations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WorkspaceKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                PluginId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_workspace_plugin_activations", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_workspace_plugin_activations_PluginId",
            table: "workspace_plugin_activations",
            column: "PluginId");

        migrationBuilder.CreateIndex(
            name: "IX_workspace_plugin_activations_WorkspaceKey_PluginId",
            table: "workspace_plugin_activations",
            columns: new[] { "WorkspaceKey", "PluginId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "workspace_plugin_activations");
    }
}
