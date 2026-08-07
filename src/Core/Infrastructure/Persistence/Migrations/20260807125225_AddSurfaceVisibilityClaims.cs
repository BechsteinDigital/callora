using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSurfaceVisibilityClaims : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "required_claims",
            table: "workspace_surfaces",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "required_claims",
            table: "workspace_surfaces");
    }
}
