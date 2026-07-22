using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class RemodelSipConnectionAuth : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "auth_id",
            schema: "plugin_communication",
            table: "sip_accounts");

        migrationBuilder.DropColumn(
            name: "auth_username",
            schema: "plugin_communication",
            table: "sip_accounts");

        migrationBuilder.DropColumn(
            name: "password_secret_ref",
            schema: "plugin_communication",
            table: "sip_accounts");

        migrationBuilder.AlterColumn<int>(
            name: "registration_expiry_seconds",
            schema: "plugin_communication",
            table: "sip_accounts",
            type: "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AddColumn<string>(
            name: "authentication",
            schema: "plugin_communication",
            table: "sip_accounts",
            type: "text",
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "authentication",
            schema: "plugin_communication",
            table: "sip_accounts");

        migrationBuilder.AlterColumn<int>(
            name: "registration_expiry_seconds",
            schema: "plugin_communication",
            table: "sip_accounts",
            type: "integer",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        migrationBuilder.AddColumn<string>(
            name: "auth_id",
            schema: "plugin_communication",
            table: "sip_accounts",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "auth_username",
            schema: "plugin_communication",
            table: "sip_accounts",
            type: "character varying(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "password_secret_ref",
            schema: "plugin_communication",
            table: "sip_accounts",
            type: "character varying(500)",
            maxLength: 500,
            nullable: false,
            defaultValue: "");
    }
}
