using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSurfaceTree : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "parent_surface_id",
            table: "workspace_surfaces",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "position",
            table: "workspace_surfaces",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateIndex(
            name: "IX_workspace_surfaces_parent_surface_id_position",
            table: "workspace_surfaces",
            columns: new[] { "parent_surface_id", "position" });

        migrationBuilder.AddForeignKey(
            name: "FK_workspace_surfaces_workspace_surfaces_parent_surface_id",
            table: "workspace_surfaces",
            column: "parent_surface_id",
            principalTable: "workspace_surfaces",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_workspace_surfaces_workspace_surfaces_parent_surface_id",
            table: "workspace_surfaces");

        migrationBuilder.DropIndex(
            name: "IX_workspace_surfaces_parent_surface_id_position",
            table: "workspace_surfaces");

        migrationBuilder.DropColumn(
            name: "parent_surface_id",
            table: "workspace_surfaces");

        migrationBuilder.DropColumn(
            name: "position",
            table: "workspace_surfaces");
    }
}
