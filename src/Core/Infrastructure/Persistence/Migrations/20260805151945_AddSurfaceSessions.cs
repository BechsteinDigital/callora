using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSurfaceSessions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "surface_sessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                workspace_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                surface_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                audience = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                subject_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                claims_json = table.Column<string>(type: "text", nullable: false),
                authentication_method = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                authenticated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                issued_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_seen_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                identity_plugin_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                identity_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_surface_sessions", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_surface_sessions_expires_at_utc",
            table: "surface_sessions",
            column: "expires_at_utc");

        migrationBuilder.CreateIndex(
            name: "IX_surface_sessions_workspace_key_surface_key",
            table: "surface_sessions",
            columns: new[] { "workspace_key", "surface_key" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "surface_sessions");
    }
}
