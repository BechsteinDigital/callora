using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSessionResumeTickets : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "session_resume_tickets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                plugin_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                session_kind = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                workspace_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                payload = table.Column<string>(type: "text", nullable: false),
                issued_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_session_resume_tickets", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_session_resume_tickets_expires_at_utc",
            table: "session_resume_tickets",
            column: "expires_at_utc");

        migrationBuilder.CreateIndex(
            name: "IX_session_resume_tickets_token_hash",
            table: "session_resume_tickets",
            column: "token_hash",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "session_resume_tickets");
    }
}
