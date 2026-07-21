using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddMediaStreamSessions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "media_stream_sessions",
            schema: "plugin_communication",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                CallId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                WorkspaceKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                ConsumerRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ConnectToken = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                audio_codec = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                audio_sample_rate_hz = table.Column<int>(type: "integer", nullable: false),
                audio_frame_ms = table.Column<int>(type: "integer", nullable: false),
                Direction = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                Status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_media_stream_sessions", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_media_stream_sessions_ConnectToken",
            schema: "plugin_communication",
            table: "media_stream_sessions",
            column: "ConnectToken",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_media_stream_sessions_WorkspaceKey",
            schema: "plugin_communication",
            table: "media_stream_sessions",
            column: "WorkspaceKey");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "media_stream_sessions",
            schema: "plugin_communication");
    }
}
