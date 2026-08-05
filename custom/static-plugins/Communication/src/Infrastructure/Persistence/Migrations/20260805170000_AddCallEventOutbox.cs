using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCallEventOutbox : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Call events are written here in the same transaction as the call-log change that
        // produced them, so an event can never describe a state the database does not hold and a
        // bus outage delays delivery instead of losing it (#113).
        migrationBuilder.CreateTable(
            name: "call_event_outbox",
            schema: "plugin_communication",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EventName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                WorkspaceKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                PayloadJson = table.Column<string>(type: "text", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Attempts = table.Column<int>(type: "integer", nullable: false),
                NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_call_event_outbox", x => x.Id);
            });

        // Serves the drain query, which filters on undelivered-and-due.
        migrationBuilder.CreateIndex(
            name: "IX_call_event_outbox_DeliveredAt_NextAttemptAt",
            schema: "plugin_communication",
            table: "call_event_outbox",
            columns: new[] { "DeliveredAt", "NextAttemptAt" });

        migrationBuilder.CreateIndex(
            name: "IX_call_event_outbox_OccurredAt",
            schema: "plugin_communication",
            table: "call_event_outbox",
            column: "OccurredAt");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "call_event_outbox",
            schema: "plugin_communication");
    }
}
