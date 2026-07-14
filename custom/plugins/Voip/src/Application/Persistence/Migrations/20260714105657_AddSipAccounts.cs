using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Plugins.Voip.src.Application.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSipAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sip_accounts",
                schema: "plugin_voip",
                columns: table => new
                {
                    WorkspaceKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SipAccountId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Username = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Domain = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProtectedSecret = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sip_accounts", x => new { x.WorkspaceKey, x.SipAccountId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sip_accounts",
                schema: "plugin_voip");
        }
    }
}
