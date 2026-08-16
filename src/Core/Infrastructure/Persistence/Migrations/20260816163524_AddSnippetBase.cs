using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSnippetBase : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "snippet_base",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PluginId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                SnippetKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Locale = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                Value = table.Column<string>(type: "text", nullable: false),
                Version = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_snippet_base", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_snippet_base_Locale",
            table: "snippet_base",
            column: "Locale");

        migrationBuilder.CreateIndex(
            name: "IX_snippet_base_PluginId_SnippetKey_Locale",
            table: "snippet_base",
            columns: new[] { "PluginId", "SnippetKey", "Locale" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "snippet_base");
    }
}
