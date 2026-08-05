using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSipAccountLastRegisteredAt : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Nullable and unseeded on purpose: "never registered" is exactly what an existing row
        // should say until the provider reports a registration (#112). Backfilling from
        // LastStatusChangeAt would invent a success that may never have happened.
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LastRegisteredAt",
            schema: "plugin_communication",
            table: "sip_accounts",
            type: "timestamp with time zone",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LastRegisteredAt",
            schema: "plugin_communication",
            table: "sip_accounts");
    }
}
