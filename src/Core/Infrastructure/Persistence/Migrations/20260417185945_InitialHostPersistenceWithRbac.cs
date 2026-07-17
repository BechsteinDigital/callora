using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialHostPersistenceWithRbac : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "backend_rbac_roles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_backend_rbac_roles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "plugin_audit_logs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Action = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                PluginId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                RequestedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_plugin_audit_logs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "plugin_installations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PluginId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                AssemblyPath = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                EntryTypeName = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                State = table.Column<int>(type: "integer", nullable: false),
                InstalledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_plugin_installations", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "backend_rbac_role_permissions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                PermissionKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_backend_rbac_role_permissions", x => x.Id);
                table.ForeignKey(
                    name: "FK_backend_rbac_role_permissions_backend_rbac_roles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "backend_rbac_roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "backend_rbac_user_roles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                AssignedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_backend_rbac_user_roles", x => x.Id);
                table.ForeignKey(
                    name: "FK_backend_rbac_user_roles_backend_rbac_roles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "backend_rbac_roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_backend_rbac_role_permissions_RoleId_PermissionKey",
            table: "backend_rbac_role_permissions",
            columns: new[] { "RoleId", "PermissionKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_backend_rbac_roles_Name",
            table: "backend_rbac_roles",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_backend_rbac_user_roles_RoleId",
            table: "backend_rbac_user_roles",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "IX_backend_rbac_user_roles_UserId",
            table: "backend_rbac_user_roles",
            column: "UserId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_plugin_audit_logs_OccurredAtUtc",
            table: "plugin_audit_logs",
            column: "OccurredAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_plugin_audit_logs_PluginId",
            table: "plugin_audit_logs",
            column: "PluginId");

        migrationBuilder.CreateIndex(
            name: "IX_plugin_installations_PluginId",
            table: "plugin_installations",
            column: "PluginId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "backend_rbac_role_permissions");

        migrationBuilder.DropTable(
            name: "backend_rbac_user_roles");

        migrationBuilder.DropTable(
            name: "plugin_audit_logs");

        migrationBuilder.DropTable(
            name: "plugin_installations");

        migrationBuilder.DropTable(
            name: "backend_rbac_roles");
    }
}
