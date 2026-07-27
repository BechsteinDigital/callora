using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddTrunkInboundFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "inbound_numbers",
            schema: "plugin_communication",
            table: "sip_accounts",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "outbound_proxy",
            schema: "plugin_communication",
            table: "sip_accounts",
            type: "character varying(255)",
            maxLength: 255,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "inbound_numbers",
            schema: "plugin_communication",
            table: "sip_accounts");

        migrationBuilder.DropColumn(
            name: "outbound_proxy",
            schema: "plugin_communication",
            table: "sip_accounts");
    }
}
