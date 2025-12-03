using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Base.Host.PostgreSqlMigrations.Data.Migrations.Roles
{
    /// <inheritdoc />
    public partial class InitialPostgresHostRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Host_AuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Host_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Host_Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Host_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Host_RbacRoles",
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
                    table.PrimaryKey("PK_Host_RbacRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Host_RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Host_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_Host_RolePermissions_Host_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Host_Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Host_RolePermissions_Host_RbacRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Host_RbacRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Host_UserRolesRbac",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Host_UserRolesRbac", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_Host_UserRolesRbac_Host_RbacRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Host_RbacRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Host_AuditEntries_ActorUserId",
                table: "Host_AuditEntries",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Host_AuditEntries_CreatedAt",
                table: "Host_AuditEntries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Host_AuditEntries_TargetUserId",
                table: "Host_AuditEntries",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Host_Permissions_Name",
                table: "Host_Permissions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Host_RbacRoles_Name",
                table: "Host_RbacRoles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Host_RolePermissions_PermissionId",
                table: "Host_RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Host_UserRolesRbac_RoleId",
                table: "Host_UserRolesRbac",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Host_AuditEntries");

            migrationBuilder.DropTable(
                name: "Host_RolePermissions");

            migrationBuilder.DropTable(
                name: "Host_UserRolesRbac");

            migrationBuilder.DropTable(
                name: "Host_Permissions");

            migrationBuilder.DropTable(
                name: "Host_RbacRoles");
        }
    }
}
