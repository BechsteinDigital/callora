using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MigrateAdminRoleToSuperAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data migration (no schema change): the global "admin" system role
            // is renamed to "superadmin" so existing global admins keep their
            // full access. "admin" is now a per-workspace role. Idempotent and
            // safe whether or not the seeder already created "superadmin".

            // Case A — no superadmin yet: rename the admin system role in place,
            // so every assignment (via RoleId) carries over untouched.
            migrationBuilder.Sql(@"
                UPDATE backend_rbac_roles
                SET ""Name"" = 'superadmin'
                WHERE ""Name"" = 'admin' AND ""IsSystem"" = TRUE
                  AND NOT EXISTS (SELECT 1 FROM backend_rbac_roles WHERE ""Name"" = 'superadmin');");

            // Case B — superadmin already exists: repoint admin assignments to
            // superadmin, then drop the redundant admin system role.
            migrationBuilder.Sql(@"
                UPDATE backend_rbac_user_roles ur
                SET ""RoleId"" = sa.""Id""
                FROM backend_rbac_roles sa, backend_rbac_roles a
                WHERE sa.""Name"" = 'superadmin'
                  AND a.""Name"" = 'admin' AND a.""IsSystem"" = TRUE
                  AND ur.""RoleId"" = a.""Id"";");

            migrationBuilder.Sql(@"
                DELETE FROM backend_rbac_role_permissions
                WHERE ""RoleId"" IN (
                    SELECT ""Id"" FROM backend_rbac_roles WHERE ""Name"" = 'admin' AND ""IsSystem"" = TRUE);");

            migrationBuilder.Sql(@"
                DELETE FROM backend_rbac_roles
                WHERE ""Name"" = 'admin' AND ""IsSystem"" = TRUE
                  AND EXISTS (SELECT 1 FROM backend_rbac_roles WHERE ""Name"" = 'superadmin');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort reversal: rename superadmin back to admin when no
            // admin system role exists. The Case B merge is not restored.
            migrationBuilder.Sql(@"
                UPDATE backend_rbac_roles
                SET ""Name"" = 'admin'
                WHERE ""Name"" = 'superadmin' AND ""IsSystem"" = TRUE
                  AND NOT EXISTS (SELECT 1 FROM backend_rbac_roles WHERE ""Name"" = 'admin' AND ""IsSystem"" = TRUE);");
        }
    }
}
