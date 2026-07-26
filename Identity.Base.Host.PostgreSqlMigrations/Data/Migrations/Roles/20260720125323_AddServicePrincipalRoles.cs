using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Base.Host.PostgreSqlMigrations.Data.Migrations.Roles
{
    /// <inheritdoc />
    public partial class AddServicePrincipalRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Host_ServicePrincipalRoles",
                columns: table => new
                {
                    ServicePrincipalId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Host_ServicePrincipalRoles", x => new { x.ServicePrincipalId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_Host_ServicePrincipalRoles_Host_RbacRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Host_RbacRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Host_ServicePrincipalRoles_RoleId",
                table: "Host_ServicePrincipalRoles",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Host_ServicePrincipalRoles");
        }
    }
}
