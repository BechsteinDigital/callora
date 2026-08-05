using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class CallLogSurrogateKey : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The provider's call id was the primary key, but it is unique only inside its own
        // channel: two channels reporting the same id collided on insert, so the second and
        // entirely legitimate call could not be recorded (#113). Existing rows keep their data
        // and receive a generated key.
        migrationBuilder.AddColumn<Guid>(
            name: "RecordId",
            schema: "plugin_communication",
            table: "call_logs",
            type: "uuid",
            nullable: false,
            defaultValueSql: "gen_random_uuid()");

        migrationBuilder.DropPrimaryKey(
            name: "PK_call_logs",
            schema: "plugin_communication",
            table: "call_logs");

        migrationBuilder.AddPrimaryKey(
            name: "PK_call_logs",
            schema: "plugin_communication",
            table: "call_logs",
            column: "RecordId");

        // Uniqueness moves to the identity the provider actually guarantees.
        migrationBuilder.CreateIndex(
            name: "IX_call_logs_WorkspaceKey_AccountId_Id",
            schema: "plugin_communication",
            table: "call_logs",
            columns: new[] { "WorkspaceKey", "AccountId", "Id" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_call_logs_WorkspaceKey_AccountId_Id",
            schema: "plugin_communication",
            table: "call_logs");

        migrationBuilder.DropPrimaryKey(
            name: "PK_call_logs",
            schema: "plugin_communication",
            table: "call_logs");

        // Reverting requires the call id to be globally unique again; rows that only coexisted
        // thanks to the surrogate key are dropped rather than silently merged.
        migrationBuilder.Sql(
            """
            DELETE FROM plugin_communication.call_logs a
            USING plugin_communication.call_logs b
            WHERE a."Id" = b."Id" AND a."RecordId" > b."RecordId";
            """);

        migrationBuilder.AddPrimaryKey(
            name: "PK_call_logs",
            schema: "plugin_communication",
            table: "call_logs",
            column: "Id");

        migrationBuilder.DropColumn(
            name: "RecordId",
            schema: "plugin_communication",
            table: "call_logs");
    }
}
