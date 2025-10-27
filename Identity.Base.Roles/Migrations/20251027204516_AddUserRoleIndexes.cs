using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Base.Roles.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRoleIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_Identity_UserRolesRbac_RoleId"
                ON "Identity_UserRolesRbac" ("RoleId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_Identity_UserRolesRbac_RoleId";
                """);
        }
    }
}
