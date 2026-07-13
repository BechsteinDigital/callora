using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Host.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeparatePluginEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "plugin_entitlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    WorkspaceKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IsEntitled = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plugin_entitlements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_plugin_entitlements_PluginId_TenantKey_WorkspaceKey",
                table: "plugin_entitlements",
                columns: new[] { "PluginId", "TenantKey", "WorkspaceKey" },
                unique: true);

            // Datenübernahme: Aktivierung diente bisher zugleich als
            // Entitlement — bestehende aktive Aktivierungen werden konservativ
            // als Grants übernommen, damit sich das Verhalten nicht ändert
            // (PLAT-253).
            migrationBuilder.Sql("""
                INSERT INTO plugin_entitlements
                    ("Id", "PluginId", "TenantKey", "WorkspaceKey", "IsEntitled", "Source", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT gen_random_uuid(), a."PluginId", a."TenantKey", a."WorkspaceKey", true, 'migrated', now(), now()
                FROM workspace_plugin_activations a
                WHERE a."IsActive" = true;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plugin_entitlements");
        }
    }
}
