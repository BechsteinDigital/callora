using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBackendUsersWithCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "backend_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    PasswordHash = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    PasswordHashAlgorithm = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backend_users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_backend_users_Email",
                table: "backend_users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_backend_users_ExternalId",
                table: "backend_users",
                column: "ExternalId",
                unique: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserIdNew",
                table: "backend_rbac_user_roles",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                INSERT INTO backend_users ("Id", "ExternalId", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT
                    (
                        substring(md5(btrim(ur."UserId")) from 1 for 8) || '-' ||
                        substring(md5(btrim(ur."UserId")) from 9 for 4) || '-' ||
                        substring(md5(btrim(ur."UserId")) from 13 for 4) || '-' ||
                        substring(md5(btrim(ur."UserId")) from 17 for 4) || '-' ||
                        substring(md5(btrim(ur."UserId")) from 21 for 12)
                    )::uuid,
                    btrim(ur."UserId"),
                    NOW(),
                    NOW()
                FROM backend_rbac_user_roles ur
                WHERE btrim(ur."UserId") <> ''
                ON CONFLICT ("ExternalId") DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                UPDATE backend_rbac_user_roles ur
                SET "UserIdNew" = u."Id"
                FROM backend_users u
                WHERE btrim(ur."UserId") = u."ExternalId";
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM backend_rbac_user_roles
                WHERE "UserIdNew" IS NULL;
                """);

            migrationBuilder.DropIndex(
                name: "IX_backend_rbac_user_roles_UserId",
                table: "backend_rbac_user_roles");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "backend_rbac_user_roles");

            migrationBuilder.RenameColumn(
                name: "UserIdNew",
                table: "backend_rbac_user_roles",
                newName: "UserId");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "backend_rbac_user_roles",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_backend_rbac_user_roles_UserId",
                table: "backend_rbac_user_roles",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_backend_rbac_user_roles_backend_users_UserId",
                table: "backend_rbac_user_roles",
                column: "UserId",
                principalTable: "backend_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_backend_rbac_user_roles_backend_users_UserId",
                table: "backend_rbac_user_roles");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "backend_rbac_user_roles",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.DropIndex(
                name: "IX_backend_rbac_user_roles_UserId",
                table: "backend_rbac_user_roles");

            migrationBuilder.AddColumn<string>(
                name: "LegacyUserId",
                table: "backend_rbac_user_roles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE backend_rbac_user_roles ur
                SET "LegacyUserId" = u."ExternalId"
                FROM backend_users u
                WHERE ur."UserId" = u."Id";
                """);

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "backend_rbac_user_roles");

            migrationBuilder.RenameColumn(
                name: "LegacyUserId",
                table: "backend_rbac_user_roles",
                newName: "UserId");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "backend_rbac_user_roles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: string.Empty,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_backend_rbac_user_roles_UserId",
                table: "backend_rbac_user_roles",
                column: "UserId",
                unique: true);

            migrationBuilder.DropTable(
                name: "backend_users");
        }
    }
}
