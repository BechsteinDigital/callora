using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Host.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantManagementCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_workspace_plugin_activations_WorkspaceKey_PluginId",
                table: "workspace_plugin_activations");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "workspaces",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "TenantKey",
                table: "workspace_plugin_activations",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workspaces_TenantId",
                table: "workspaces",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_plugin_activations_TenantKey_PluginId",
                table: "workspace_plugin_activations",
                columns: new[] { "TenantKey", "PluginId" });

            migrationBuilder.CreateIndex(
                name: "IX_workspace_plugin_activations_TenantKey_WorkspaceKey_PluginId",
                table: "workspace_plugin_activations",
                columns: new[] { "TenantKey", "WorkspaceKey", "PluginId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_TenantKey",
                table: "tenants",
                column: "TenantKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_workspaces_tenants_TenantId",
                table: "workspaces",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_workspaces_tenants_TenantId",
                table: "workspaces");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropIndex(
                name: "IX_workspaces_TenantId",
                table: "workspaces");

            migrationBuilder.DropIndex(
                name: "IX_workspace_plugin_activations_TenantKey_PluginId",
                table: "workspace_plugin_activations");

            migrationBuilder.DropIndex(
                name: "IX_workspace_plugin_activations_TenantKey_WorkspaceKey_PluginId",
                table: "workspace_plugin_activations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "workspaces");

            migrationBuilder.DropColumn(
                name: "TenantKey",
                table: "workspace_plugin_activations");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_plugin_activations_WorkspaceKey_PluginId",
                table: "workspace_plugin_activations",
                columns: new[] { "WorkspaceKey", "PluginId" },
                unique: true);
        }
    }
}
