using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSipAccountCallQuotas : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Nullable and unseeded: an existing trunk is undivided, and that is not the same as one
        // divided into zero shares. NULL is what "no split configured" looks like, and every origin
        // stays unlimited until an operator says otherwise.
        migrationBuilder.AddColumn<string>(
            name: "call_quotas",
            schema: "plugin_communication",
            table: "sip_accounts",
            type: "text",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "call_quotas",
            schema: "plugin_communication",
            table: "sip_accounts");
    }
}
