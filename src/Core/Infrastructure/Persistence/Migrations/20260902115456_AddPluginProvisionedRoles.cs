using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddPluginProvisionedRoles : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ProvisionedAs",
            table: "backend_rbac_roles",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProvisionedByPluginId",
            table: "backend_rbac_roles",
            type: "character varying(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_backend_rbac_roles_ProvisionedByPluginId_ProvisionedAs",
            table: "backend_rbac_roles",
            columns: new[] { "ProvisionedByPluginId", "ProvisionedAs" },
            unique: true,
            filter: "\"ProvisionedByPluginId\" IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_backend_rbac_roles_ProvisionedByPluginId_ProvisionedAs",
            table: "backend_rbac_roles");

        migrationBuilder.DropColumn(
            name: "ProvisionedAs",
            table: "backend_rbac_roles");

        migrationBuilder.DropColumn(
            name: "ProvisionedByPluginId",
            table: "backend_rbac_roles");
    }
}
