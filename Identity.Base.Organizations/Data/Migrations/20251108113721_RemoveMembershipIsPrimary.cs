using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Base.Organizations.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMembershipIsPrimary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPrimary",
                table: "Identity_OrganizationMemberships");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPrimary",
                table: "Identity_OrganizationMemberships",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
