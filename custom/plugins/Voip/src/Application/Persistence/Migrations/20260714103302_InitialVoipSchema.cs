using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Plugins.Voip.src.Application.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialVoipSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "plugin_voip");

            migrationBuilder.CreateTable(
                name: "call_logs",
                schema: "plugin_voip",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CallId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChannelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TargetValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TargetDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_call_logs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_call_logs_WorkspaceKey_StartedAtUtc",
                schema: "plugin_voip",
                table: "call_logs",
                columns: new[] { "WorkspaceKey", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "call_logs",
                schema: "plugin_voip");
        }
    }
}
