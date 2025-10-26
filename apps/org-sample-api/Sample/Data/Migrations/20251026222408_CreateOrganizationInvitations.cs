using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrgSampleApi.Sample.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateOrganizationInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Identity_OrganizationInvitations",
                columns: table => new
                {
                    Code = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationSlug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OrganizationName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RoleIds = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Identity_OrganizationInvitations", x => x.Code);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Identity_OrganizationInvitations_Email",
                table: "Identity_OrganizationInvitations",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Identity_OrganizationInvitations_OrganizationId",
                table: "Identity_OrganizationInvitations",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Identity_OrganizationInvitations");
        }
    }
}
