using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSnippetOverrides : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "snippet_overrides",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SnippetKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Locale = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ScopeKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Value = table.Column<string>(type: "text", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_snippet_overrides", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_snippet_overrides_SnippetKey_Locale_Scope_ScopeKey",
            table: "snippet_overrides",
            columns: new[] { "SnippetKey", "Locale", "Scope", "ScopeKey" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "snippet_overrides");
    }
}
