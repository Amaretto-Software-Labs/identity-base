using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrgSampleApi.Hosting.Infrastructure.Migrations.App
{
    /// <inheritdoc />
    public partial class SyncAppModel20260208 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrgSample_Users_NormalizedEmail",
                table: "OrgSample_Users");

            migrationBuilder.DropIndex(
                name: "IX_OrgSample_Users_NormalizedUserName",
                table: "OrgSample_Users");

            migrationBuilder.DropIndex(
                name: "IX_OrgSample_Roles_NormalizedName",
                table: "OrgSample_Roles");

            migrationBuilder.AlterColumn<string>(
                name: "ProfileMetadata",
                table: "OrgSample_Users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldDefaultValueSql: "'{}'::jsonb");

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_Users_NormalizedEmail",
                table: "OrgSample_Users",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_Users_NormalizedUserName",
                table: "OrgSample_Users",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_Roles_NormalizedName",
                table: "OrgSample_Roles",
                column: "NormalizedName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrgSample_Users_NormalizedEmail",
                table: "OrgSample_Users");

            migrationBuilder.DropIndex(
                name: "IX_OrgSample_Users_NormalizedUserName",
                table: "OrgSample_Users");

            migrationBuilder.DropIndex(
                name: "IX_OrgSample_Roles_NormalizedName",
                table: "OrgSample_Roles");

            migrationBuilder.AlterColumn<string>(
                name: "ProfileMetadata",
                table: "OrgSample_Users",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_Users_NormalizedEmail",
                table: "OrgSample_Users",
                column: "NormalizedEmail",
                unique: true,
                filter: "\"NormalizedEmail\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_Users_NormalizedUserName",
                table: "OrgSample_Users",
                column: "NormalizedUserName",
                unique: true,
                filter: "\"NormalizedUserName\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_Roles_NormalizedName",
                table: "OrgSample_Roles",
                column: "NormalizedName",
                unique: true,
                filter: "\"NormalizedName\" IS NOT NULL");
        }
    }
}
