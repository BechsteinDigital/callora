using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddTenantPluginDelegations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tenant_plugin_delegations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                PluginId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                WorkspacesMayAssign = table.Column<bool>(type: "boolean", nullable: false),
                UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tenant_plugin_delegations", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_tenant_plugin_delegations_TenantKey_PluginId",
            table: "tenant_plugin_delegations",
            columns: new[] { "TenantKey", "PluginId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "tenant_plugin_delegations");
    }
}
