using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCommunicationSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "plugin_communication");

        migrationBuilder.CreateTable(
            name: "call_logs",
            schema: "plugin_communication",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                WorkspaceKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                AccountId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                LineId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                RemoteParty = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                LocalIdentity = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                HandledBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                AnsweredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                DisconnectCause = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_call_logs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "sip_accounts",
            schema: "plugin_communication",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                WorkspaceKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                port = table.Column<int>(type: "integer", nullable: false),
                transport = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                auth_username = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                auth_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                password_secret_ref = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                registration_expiry_seconds = table.Column<int>(type: "integer", nullable: false),
                MaxConcurrentCalls = table.Column<int>(type: "integer", nullable: false),
                Enabled = table.Column<bool>(type: "boolean", nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                LastStatusChangeAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_sip_accounts", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "sip_lines",
            schema: "plugin_communication",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                AccountId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                WorkspaceKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                SipUri = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                PrimaryNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                Enabled = table.Column<bool>(type: "boolean", nullable: false),
                InboundRoutingTarget = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_sip_lines", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_call_logs_WorkspaceKey_StartedAt",
            schema: "plugin_communication",
            table: "call_logs",
            columns: new[] { "WorkspaceKey", "StartedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_sip_accounts_WorkspaceKey",
            schema: "plugin_communication",
            table: "sip_accounts",
            column: "WorkspaceKey");

        migrationBuilder.CreateIndex(
            name: "IX_sip_lines_AccountId",
            schema: "plugin_communication",
            table: "sip_lines",
            column: "AccountId");

        migrationBuilder.CreateIndex(
            name: "IX_sip_lines_WorkspaceKey",
            schema: "plugin_communication",
            table: "sip_lines",
            column: "WorkspaceKey");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "call_logs",
            schema: "plugin_communication");

        migrationBuilder.DropTable(
            name: "sip_accounts",
            schema: "plugin_communication");

        migrationBuilder.DropTable(
            name: "sip_lines",
            schema: "plugin_communication");
    }
}
