using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSurfaceRouting : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // "Tree", nicht "" — der Wertkonverter liest den Spaltentext als Enum-Namen, und ein
        // leerer String (EFs Vorgabe für eine nicht-nullable Zeichenkette) wäre kein gültiger
        // Name. Jede Bestandszeile käme beim ersten Lesen als Ausnahme zurück.
        //
        // Und inhaltlich ist Tree richtig: Bestandsflächen sind Websites. Wer eine Anwendung
        // betreibt, die ihre Pfade selbst deutet, stellt sie um — ausdrücklich, statt dass
        // eine Migration es für ihn annimmt.
        migrationBuilder.AddColumn<string>(
            name: "routing",
            table: "workspace_surfaces",
            type: "character varying(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "Tree");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "routing",
            table: "workspace_surfaces");
    }
}
