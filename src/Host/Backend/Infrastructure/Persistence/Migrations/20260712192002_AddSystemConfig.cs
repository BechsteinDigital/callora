using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Host.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "system_config_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Version = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ConfigKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Label = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    FieldType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DefaultValueJson = table.Column<string>(type: "text", nullable: true),
                    GroupName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    OptionsJson = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_config_definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "system_config_values",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ConfigKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ScopeKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ValueJson = table.Column<string>(type: "text", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_config_values", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_system_config_definitions_PluginId_ConfigKey",
                table: "system_config_definitions",
                columns: new[] { "PluginId", "ConfigKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_system_config_values_PluginId_ConfigKey_Scope_ScopeKey",
                table: "system_config_values",
                columns: new[] { "PluginId", "ConfigKey", "Scope", "ScopeKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_config_definitions");

            migrationBuilder.DropTable(
                name: "system_config_values");
        }
    }
}
