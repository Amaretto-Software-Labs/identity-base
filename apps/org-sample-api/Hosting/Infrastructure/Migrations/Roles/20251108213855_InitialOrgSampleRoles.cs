using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrgSampleApi.Hosting.Infrastructure.Migrations.Roles
{
    /// <inheritdoc />
    public partial class InitialOrgSampleRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrgSample_AuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgSample_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrgSample_Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgSample_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrgSample_RbacRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsSystemRole = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgSample_RbacRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrgSample_RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgSample_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_OrgSample_RolePermissions_OrgSample_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "OrgSample_Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrgSample_RolePermissions_OrgSample_RbacRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "OrgSample_RbacRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrgSample_UserRolesRbac",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgSample_UserRolesRbac", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_OrgSample_UserRolesRbac_OrgSample_RbacRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "OrgSample_RbacRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_AuditEntries_ActorUserId",
                table: "OrgSample_AuditEntries",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_AuditEntries_CreatedAt",
                table: "OrgSample_AuditEntries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_AuditEntries_TargetUserId",
                table: "OrgSample_AuditEntries",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_Permissions_Name",
                table: "OrgSample_Permissions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_RbacRoles_Name",
                table: "OrgSample_RbacRoles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_RolePermissions_PermissionId",
                table: "OrgSample_RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_UserRolesRbac_RoleId",
                table: "OrgSample_UserRolesRbac",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrgSample_AuditEntries");

            migrationBuilder.DropTable(
                name: "OrgSample_RolePermissions");

            migrationBuilder.DropTable(
                name: "OrgSample_UserRolesRbac");

            migrationBuilder.DropTable(
                name: "OrgSample_Permissions");

            migrationBuilder.DropTable(
                name: "OrgSample_RbacRoles");
        }
    }
}
