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
            migrationBuilder.CreateIndex(
                name: "IX_Identity_UserRolesRbac_RoleId",
                table: "Identity_UserRolesRbac",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Identity_UserRolesRbac_RoleId",
                table: "Identity_UserRolesRbac");
        }
    }
}
