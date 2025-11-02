using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Base.Organisations.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganisationRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS \"uuid-ossp\";");

            migrationBuilder.CreateTable(
                name: "Identity_OrganisationRolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Identity_OrganisationRolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Identity_OrganisationRolePermissions_Identity_OrganisationR~",
                        column: x => x.RoleId,
                        principalTable: "Identity_OrganisationRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationRolePermissions_Organisation_Role",
                table: "Identity_OrganisationRolePermissions",
                columns: new[] { "OrganisationId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationRolePermissions_Role_Permission",
                table: "Identity_OrganisationRolePermissions",
                columns: new[] { "RoleId", "PermissionId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationRolePermissions_Tenant_Role",
                table: "Identity_OrganisationRolePermissions",
                columns: new[] { "TenantId", "RoleId" });

            SeedDefaultRolePermissions(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Identity_OrganisationRolePermissions");
        }

        private static void SeedDefaultRolePermissions(MigrationBuilder migrationBuilder)
        {
            InsertPermissions(migrationBuilder, "OrgOwner", "('organisations.read','organisations.manage','organisation.members.read','organisation.members.manage','organisation.roles.read','organisation.roles.manage')");
            InsertPermissions(migrationBuilder, "OrgManager", "('organisations.read','organisation.members.read','organisation.members.manage','organisation.roles.read')");
            InsertPermissions(migrationBuilder, "OrgMember", "('organisations.read')");
        }

        private static void InsertPermissions(MigrationBuilder migrationBuilder, string roleName, string permissionTuple)
        {
            migrationBuilder.Sql($@"
INSERT INTO ""Identity_OrganisationRolePermissions"" (""RoleId"", ""PermissionId"", ""TenantId"", ""OrganisationId"", ""CreatedAtUtc"")
SELECT role.""Id"", permission.""Id"", role.""TenantId"", role.""OrganisationId"", CURRENT_TIMESTAMP
FROM ""Identity_OrganisationRoles"" AS role
JOIN ""Identity_Permissions"" AS permission ON permission.""Name"" IN {permissionTuple}
LEFT JOIN ""Identity_OrganisationRolePermissions"" AS existing
    ON existing.""RoleId"" = role.""Id""
   AND existing.""PermissionId"" = permission.""Id""
   AND (existing.""TenantId"" IS NOT DISTINCT FROM role.""TenantId"")
   AND (existing.""OrganisationId"" IS NOT DISTINCT FROM role.""OrganisationId"")
WHERE role.""Name"" = '{roleName}'
  AND existing.""Id"" IS NULL;
");
        }
    }
}
