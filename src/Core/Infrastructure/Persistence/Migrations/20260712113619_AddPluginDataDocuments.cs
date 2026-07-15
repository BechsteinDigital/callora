using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPluginDataDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "plugin_data_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WorkspaceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Collection = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EntryKey = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    JsonDocument = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plugin_data_documents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_plugin_data_documents_PluginId_WorkspaceKey_Collection_Entr~",
                table: "plugin_data_documents",
                columns: new[] { "PluginId", "WorkspaceKey", "Collection", "EntryKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plugin_data_documents");
        }
    }
}
