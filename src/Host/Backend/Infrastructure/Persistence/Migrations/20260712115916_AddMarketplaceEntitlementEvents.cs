using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Host.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceEntitlementEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "marketplace_entitlement_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PluginId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WorkspaceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_entitlement_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_entitlement_events_EventId",
                table: "marketplace_entitlement_events",
                column: "EventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "marketplace_entitlement_events");
        }
    }
}
