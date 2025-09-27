using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Base.Roles.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentityRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Identity_AuditEntries",
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
                    table.PrimaryKey("PK_Identity_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Identity_Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Identity_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Identity_RbacRoles",
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
                    table.PrimaryKey("PK_Identity_RbacRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Identity_RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Identity_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_Identity_RolePermissions_Identity_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Identity_Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Identity_RolePermissions_Identity_RbacRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Identity_RbacRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Identity_UserRolesRbac",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Identity_UserRolesRbac", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_Identity_UserRolesRbac_Identity_RbacRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Identity_RbacRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Identity_AuditEntries_ActorUserId",
                table: "Identity_AuditEntries",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Identity_AuditEntries_CreatedAt",
                table: "Identity_AuditEntries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Identity_AuditEntries_TargetUserId",
                table: "Identity_AuditEntries",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Identity_Permissions_Name",
                table: "Identity_Permissions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Identity_RbacRoles_Name",
                table: "Identity_RbacRoles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Identity_RolePermissions_PermissionId",
                table: "Identity_RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Identity_UserRolesRbac_RoleId",
                table: "Identity_UserRolesRbac",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Identity_AuditEntries");

            migrationBuilder.DropTable(
                name: "Identity_RolePermissions");

            migrationBuilder.DropTable(
                name: "Identity_UserRolesRbac");

            migrationBuilder.DropTable(
                name: "Identity_Permissions");

            migrationBuilder.DropTable(
                name: "Identity_RbacRoles");
        }
    }
}
