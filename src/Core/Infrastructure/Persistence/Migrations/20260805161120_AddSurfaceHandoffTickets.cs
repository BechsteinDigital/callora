using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSurfaceHandoffTickets : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "surface_handoff_tickets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                tenant_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                workspace_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                source_surface_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                target_surface_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                target_audience = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                subject_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                claims_json = table.Column<string>(type: "text", nullable: false),
                authentication_method = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                authenticated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                identity_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                issued_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_surface_handoff_tickets", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_surface_handoff_tickets_expires_at_utc",
            table: "surface_handoff_tickets",
            column: "expires_at_utc");

        migrationBuilder.CreateIndex(
            name: "IX_surface_handoff_tickets_token_hash",
            table: "surface_handoff_tickets",
            column: "token_hash",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "surface_handoff_tickets");
    }
}
