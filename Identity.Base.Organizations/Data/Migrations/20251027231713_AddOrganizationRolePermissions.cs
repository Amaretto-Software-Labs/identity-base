using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Base.Organizations.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS \"uuid-ossp\";");

            migrationBuilder.CreateTable(
                name: "Identity_OrganizationRolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Identity_OrganizationRolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Identity_OrganizationRolePermissions_Identity_OrganizationR~",
                        column: x => x.RoleId,
                        principalTable: "Identity_OrganizationRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationRolePermissions_Organization_Role",
                table: "Identity_OrganizationRolePermissions",
                columns: new[] { "OrganizationId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationRolePermissions_Role_Permission",
                table: "Identity_OrganizationRolePermissions",
                columns: new[] { "RoleId", "PermissionId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationRolePermissions_Tenant_Role",
                table: "Identity_OrganizationRolePermissions",
                columns: new[] { "TenantId", "RoleId" });

            SeedDefaultRolePermissions(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Identity_OrganizationRolePermissions");
        }

        private static void SeedDefaultRolePermissions(MigrationBuilder migrationBuilder)
        {
            InsertPermissions(migrationBuilder, "OrgOwner", "('admin.organizations.read','admin.organizations.manage','admin.organizations.members.read','admin.organizations.members.manage','admin.organizations.roles.read','admin.organizations.roles.manage','user.organizations.read','user.organizations.manage','user.organizations.members.read','user.organizations.members.manage','user.organizations.roles.read','user.organizations.roles.manage')");
            InsertPermissions(migrationBuilder, "OrgManager", "('admin.organizations.read','admin.organizations.members.read','admin.organizations.members.manage','admin.organizations.roles.read','user.organizations.read','user.organizations.members.read','user.organizations.members.manage','user.organizations.roles.read')");
            InsertPermissions(migrationBuilder, "OrgMember", "('admin.organizations.read','user.organizations.read')");
        }

        private static void InsertPermissions(MigrationBuilder migrationBuilder, string roleName, string permissionTuple)
        {
            migrationBuilder.Sql($@"
INSERT INTO ""Identity_OrganizationRolePermissions"" (""RoleId"", ""PermissionId"", ""TenantId"", ""OrganizationId"", ""CreatedAtUtc"")
SELECT role.""Id"", permission.""Id"", role.""TenantId"", role.""OrganizationId"", CURRENT_TIMESTAMP
FROM ""Identity_OrganizationRoles"" AS role
JOIN ""Identity_Permissions"" AS permission ON permission.""Name"" IN {permissionTuple}
LEFT JOIN ""Identity_OrganizationRolePermissions"" AS existing
    ON existing.""RoleId"" = role.""Id""
   AND existing.""PermissionId"" = permission.""Id""
   AND (existing.""TenantId"" IS NOT DISTINCT FROM role.""TenantId"")
   AND (existing.""OrganizationId"" IS NOT DISTINCT FROM role.""OrganizationId"")
WHERE role.""Name"" = '{roleName}'
  AND existing.""Id"" IS NULL;
");
        }
    }
}
