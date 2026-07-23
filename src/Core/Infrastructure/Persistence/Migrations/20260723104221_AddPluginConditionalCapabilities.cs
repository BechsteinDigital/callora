using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddPluginConditionalCapabilities : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ConditionalCapabilities",
            table: "plugin_installations",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ConditionalCapabilities",
            table: "plugin_installations");
    }
}
