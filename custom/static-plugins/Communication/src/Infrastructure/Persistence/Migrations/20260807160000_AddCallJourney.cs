using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Migrations;

/// <summary>
/// Gives every call room for its own story.
///
/// The history row says a call ended and how; it never said why it went where it went. An operator
/// asking "why did this ring out" had the log files of whichever plugins happened to be involved, if
/// any of them logged at all. The journey is what each participant writes down as it acts, attached
/// to the record when the call ends.
///
/// Nullable and unseeded: every existing row predates the column, and an empty journey is the honest
/// answer for a call nobody recorded anything for.
/// </summary>
/// <inheritdoc />
public partial class AddCallJourney : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "journey",
            schema: "plugin_communication",
            table: "call_logs",
            type: "text",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "journey",
            schema: "plugin_communication",
            table: "call_logs");
    }
}
