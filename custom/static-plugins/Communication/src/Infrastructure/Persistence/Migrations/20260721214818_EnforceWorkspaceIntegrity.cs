using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class EnforceWorkspaceIntegrity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_sip_lines_AccountId",
            schema: "plugin_communication",
            table: "sip_lines");

        migrationBuilder.AddUniqueConstraint(
            name: "AK_sip_accounts_WorkspaceKey_Id",
            schema: "plugin_communication",
            table: "sip_accounts",
            columns: new[] { "WorkspaceKey", "Id" });

        migrationBuilder.CreateIndex(
            name: "IX_sip_lines_WorkspaceKey_AccountId",
            schema: "plugin_communication",
            table: "sip_lines",
            columns: new[] { "WorkspaceKey", "AccountId" });

        migrationBuilder.AddForeignKey(
            name: "FK_sip_lines_sip_accounts_WorkspaceKey_AccountId",
            schema: "plugin_communication",
            table: "sip_lines",
            columns: new[] { "WorkspaceKey", "AccountId" },
            principalSchema: "plugin_communication",
            principalTable: "sip_accounts",
            principalColumns: new[] { "WorkspaceKey", "Id" },
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_sip_lines_sip_accounts_WorkspaceKey_AccountId",
            schema: "plugin_communication",
            table: "sip_lines");

        migrationBuilder.DropIndex(
            name: "IX_sip_lines_WorkspaceKey_AccountId",
            schema: "plugin_communication",
            table: "sip_lines");

        migrationBuilder.DropUniqueConstraint(
            name: "AK_sip_accounts_WorkspaceKey_Id",
            schema: "plugin_communication",
            table: "sip_accounts");

        migrationBuilder.CreateIndex(
            name: "IX_sip_lines_AccountId",
            schema: "plugin_communication",
            table: "sip_lines",
            column: "AccountId");
    }
}
