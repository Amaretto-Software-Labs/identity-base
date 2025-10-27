using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Base.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCreatedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Identity_Users_CreatedAt",
                table: "Identity_Users",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Identity_Users_CreatedAt",
                table: "Identity_Users");
        }
    }
}
