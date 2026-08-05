using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class RemoveDormantSipLines : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // SipLine had a domain type, a store and a table, but no production code ever created or
        // read one: routing, status and the admin surface all work off the account (#117). A model
        // nothing drives is worse than absent, because it reads as a capability that exists.
        // CallLog.LineId went with it; it referenced a line and was always null.
        migrationBuilder.DropColumn(
            name: "LineId",
            schema: "plugin_communication",
            table: "call_logs");

        migrationBuilder.DropTable(
            name: "sip_lines",
            schema: "plugin_communication");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
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
            name: "IX_sip_lines_AccountId",
            schema: "plugin_communication",
            table: "sip_lines",
            column: "AccountId");

        migrationBuilder.CreateIndex(
            name: "IX_sip_lines_WorkspaceKey",
            schema: "plugin_communication",
            table: "sip_lines",
            column: "WorkspaceKey");

        // The rows themselves cannot be restored; nothing wrote them in the first place.
        migrationBuilder.AddColumn<string>(
            name: "LineId",
            schema: "plugin_communication",
            table: "call_logs",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);
    }
}
