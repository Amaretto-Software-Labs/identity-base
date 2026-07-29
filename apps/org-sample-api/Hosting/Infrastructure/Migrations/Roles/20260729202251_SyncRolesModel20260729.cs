using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrgSampleApi.Hosting.Infrastructure.Migrations.Roles
{
    /// <inheritdoc />
    public partial class SyncRolesModel20260729 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrgSample_ServicePrincipalRoles",
                columns: table => new
                {
                    ServicePrincipalId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgSample_ServicePrincipalRoles", x => new { x.ServicePrincipalId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_OrgSample_ServicePrincipalRoles_OrgSample_RbacRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "OrgSample_RbacRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_ServicePrincipalRoles_RoleId",
                table: "OrgSample_ServicePrincipalRoles",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrgSample_ServicePrincipalRoles");
        }
    }
}
