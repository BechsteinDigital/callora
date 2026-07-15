using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformSubsystems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "custom_field_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Version = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    EntityName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Label = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    FieldType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_field_definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "custom_field_values",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ValueJson = table.Column<string>(type: "text", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_field_values", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "flows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    TriggerEvent = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ConditionsJson = table.Column<string>(type: "text", nullable: true),
                    ActionsJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "media_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FileName = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Folder = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "plugin_migrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AppliedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plugin_migrations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_custom_field_definitions_EntityName_FieldKey",
                table: "custom_field_definitions",
                columns: new[] { "EntityName", "FieldKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_custom_field_values_EntityName_EntityId_FieldKey",
                table: "custom_field_values",
                columns: new[] { "EntityName", "EntityId", "FieldKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_flows_WorkspaceKey_TriggerEvent_IsActive",
                table: "flows",
                columns: new[] { "WorkspaceKey", "TriggerEvent", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_media_items_WorkspaceKey_Folder",
                table: "media_items",
                columns: new[] { "WorkspaceKey", "Folder" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_WorkspaceKey_IsRead_CreatedAtUtc",
                table: "notifications",
                columns: new[] { "WorkspaceKey", "IsRead", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_plugin_migrations_PluginId_Version",
                table: "plugin_migrations",
                columns: new[] { "PluginId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "custom_field_definitions");

            migrationBuilder.DropTable(
                name: "custom_field_values");

            migrationBuilder.DropTable(
                name: "flows");

            migrationBuilder.DropTable(
                name: "media_items");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "plugin_migrations");
        }
    }
}
