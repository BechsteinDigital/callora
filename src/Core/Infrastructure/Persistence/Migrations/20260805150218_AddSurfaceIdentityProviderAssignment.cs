using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSurfaceIdentityProviderAssignment : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "identity_assigned_at_utc",
            table: "workspace_surfaces",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "identity_assigned_by",
            table: "workspace_surfaces",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "identity_plugin_id",
            table: "workspace_surfaces",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "identity_version",
            table: "workspace_surfaces",
            type: "character varying(80)",
            maxLength: 80,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "identity_assigned_at_utc",
            table: "workspace_surfaces");

        migrationBuilder.DropColumn(
            name: "identity_assigned_by",
            table: "workspace_surfaces");

        migrationBuilder.DropColumn(
            name: "identity_plugin_id",
            table: "workspace_surfaces");

        migrationBuilder.DropColumn(
            name: "identity_version",
            table: "workspace_surfaces");
    }
}
