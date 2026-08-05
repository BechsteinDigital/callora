using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAccountControlsAndSessionRevocation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "FailedAccessCount",
            table: "backend_users",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<bool>(
            name: "IsDisabled",
            table: "backend_users",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LockoutEndsAtUtc",
            table: "backend_users",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SecurityStamp",
            table: "backend_users",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateTable(
            name: "backend_revoked_sessions",
            columns: table => new
            {
                TokenId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_backend_revoked_sessions", x => x.TokenId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_backend_revoked_sessions_ExpiresAtUtc",
            table: "backend_revoked_sessions",
            column: "ExpiresAtUtc");

        // Backfill: an existing account must receive a real stamp, not the empty
        // default. An empty stamp matches no session (BackendSecurityStamp.Matches),
        // which would reject every request from an already-signed-in operator until
        // they log in again — including, potentially, the only operator.
        migrationBuilder.Sql(
            """
            UPDATE backend_users
            SET "SecurityStamp" = md5("Id"::text || clock_timestamp()::text)
            WHERE "SecurityStamp" = '';
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "backend_revoked_sessions");

        migrationBuilder.DropColumn(
            name: "FailedAccessCount",
            table: "backend_users");

        migrationBuilder.DropColumn(
            name: "IsDisabled",
            table: "backend_users");

        migrationBuilder.DropColumn(
            name: "LockoutEndsAtUtc",
            table: "backend_users");

        migrationBuilder.DropColumn(
            name: "SecurityStamp",
            table: "backend_users");
    }
}
