using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomFieldValueWorkspaceKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WorkspaceKey",
                table: "custom_field_values",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_custom_field_values_WorkspaceKey",
                table: "custom_field_values",
                column: "WorkspaceKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_custom_field_values_WorkspaceKey",
                table: "custom_field_values");

            migrationBuilder.DropColumn(
                name: "WorkspaceKey",
                table: "custom_field_values");
        }
    }
}
