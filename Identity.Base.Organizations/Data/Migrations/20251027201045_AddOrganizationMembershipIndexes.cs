using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Base.Organizations.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationMembershipIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberships_Organization_Created",
                table: "Identity_OrganizationMemberships",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberships_Organization_User",
                table: "Identity_OrganizationMemberships",
                columns: new[] { "OrganizationId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrganizationMemberships_Organization_Created",
                table: "Identity_OrganizationMemberships");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationMemberships_Organization_User",
                table: "Identity_OrganizationMemberships");
        }
    }
}
