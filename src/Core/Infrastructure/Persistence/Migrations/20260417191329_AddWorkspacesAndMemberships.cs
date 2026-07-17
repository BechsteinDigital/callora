using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddWorkspacesAndMemberships : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "workspaces",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WorkspaceKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                WorkspaceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_workspaces", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "workspace_memberships",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Role = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                AssignedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_workspace_memberships", x => x.Id);
                table.ForeignKey(
                    name: "FK_workspace_memberships_backend_users_UserId",
                    column: x => x.UserId,
                    principalTable: "backend_users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_workspace_memberships_workspaces_WorkspaceId",
                    column: x => x.WorkspaceId,
                    principalTable: "workspaces",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_workspace_memberships_UserId",
            table: "workspace_memberships",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_workspace_memberships_WorkspaceId_UserId",
            table: "workspace_memberships",
            columns: new[] { "WorkspaceId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_workspaces_WorkspaceKey",
            table: "workspaces",
            column: "WorkspaceKey",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "workspace_memberships");

        migrationBuilder.DropTable(
            name: "workspaces");
    }
}
