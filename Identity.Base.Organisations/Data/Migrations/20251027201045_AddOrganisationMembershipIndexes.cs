using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Base.Organisations.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganisationMembershipIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OrganisationMemberships_Organisation_Created",
                table: "Identity_OrganisationMemberships",
                columns: new[] { "OrganisationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationMemberships_Organisation_User",
                table: "Identity_OrganisationMemberships",
                columns: new[] { "OrganisationId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrganisationMemberships_Organisation_Created",
                table: "Identity_OrganisationMemberships");

            migrationBuilder.DropIndex(
                name: "IX_OrganisationMemberships_Organisation_User",
                table: "Identity_OrganisationMemberships");
        }
    }
}
