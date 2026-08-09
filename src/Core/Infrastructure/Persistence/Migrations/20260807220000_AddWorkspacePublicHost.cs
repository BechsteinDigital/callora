using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddWorkspacePublicHost : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Eine Basis-URL kann einen Workspace bezeichnen (kunde.de) oder eine Fläche
        // (portal.kunde.de). Bisher konnte nur die Fläche einen Host tragen; ein Workspace
        // ohne eigenen Host und ohne Pfadsegment beanspruchte deshalb die gesamte Origin —
        // zwei davon waren nicht unterscheidbar, und der zweite blieb unerreichbar.
        migrationBuilder.AddColumn<string>(
            name: "public_host",
            table: "workspaces",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_workspaces_public_host",
            table: "workspaces",
            column: "public_host");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_workspaces_public_host",
            table: "workspaces");

        migrationBuilder.DropColumn(
            name: "public_host",
            table: "workspaces");
    }
}
