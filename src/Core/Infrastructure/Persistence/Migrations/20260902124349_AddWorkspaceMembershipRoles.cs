using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddWorkspaceMembershipRoles : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "workspace_membership_roles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                AssignedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_workspace_membership_roles", x => x.Id);
                table.ForeignKey(
                    name: "FK_workspace_membership_roles_backend_rbac_roles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "backend_rbac_roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_workspace_membership_roles_workspace_memberships_Membership~",
                    column: x => x.MembershipId,
                    principalTable: "workspace_memberships",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_workspace_membership_roles_MembershipId_RoleId",
            table: "workspace_membership_roles",
            columns: new[] { "MembershipId", "RoleId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_workspace_membership_roles_RoleId",
            table: "workspace_membership_roles",
            column: "RoleId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "workspace_membership_roles");
    }
}
