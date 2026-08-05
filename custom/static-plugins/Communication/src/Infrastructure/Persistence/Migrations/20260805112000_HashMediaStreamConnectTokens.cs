using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class HashMediaStreamConnectTokens : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Connect tokens are no longer stored in the clear (#108): the row keeps only a
        // SHA-256 lookup key. Existing rows carry plaintext tokens that cannot be
        // converted (hashing them would be correct, but they are two-minute tickets and
        // any pending one is already worthless), so the table is emptied rather than
        // migrated — that is also the safest outcome for a leaked ticket.
        migrationBuilder.Sql(
            """
            DELETE FROM plugin_communication.media_stream_sessions;
            """);

        migrationBuilder.DropIndex(
            name: "IX_media_stream_sessions_ConnectToken",
            schema: "plugin_communication",
            table: "media_stream_sessions");

        migrationBuilder.DropColumn(
            name: "ConnectToken",
            schema: "plugin_communication",
            table: "media_stream_sessions");

        migrationBuilder.AddColumn<string>(
            name: "ConnectTokenHash",
            schema: "plugin_communication",
            table: "media_stream_sessions",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateIndex(
            name: "IX_media_stream_sessions_ConnectTokenHash",
            schema: "plugin_communication",
            table: "media_stream_sessions",
            column: "ConnectTokenHash",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Downgrading cannot restore plaintext tokens; the table is emptied again.
        migrationBuilder.Sql(
            """
            DELETE FROM plugin_communication.media_stream_sessions;
            """);

        migrationBuilder.DropIndex(
            name: "IX_media_stream_sessions_ConnectTokenHash",
            schema: "plugin_communication",
            table: "media_stream_sessions");

        migrationBuilder.DropColumn(
            name: "ConnectTokenHash",
            schema: "plugin_communication",
            table: "media_stream_sessions");

        migrationBuilder.AddColumn<string>(
            name: "ConnectToken",
            schema: "plugin_communication",
            table: "media_stream_sessions",
            type: "character varying(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateIndex(
            name: "IX_media_stream_sessions_ConnectToken",
            schema: "plugin_communication",
            table: "media_stream_sessions",
            column: "ConnectToken",
            unique: true);
    }
}
