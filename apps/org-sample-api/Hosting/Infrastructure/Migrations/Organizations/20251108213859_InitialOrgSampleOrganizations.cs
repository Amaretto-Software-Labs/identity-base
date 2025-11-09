using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrgSampleApi.Hosting.Infrastructure.Migrations.Organizations
{
    /// <inheritdoc />
    public partial class InitialOrgSampleOrganizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrgSample_OrganizationInvitations",
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
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UsedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgSample_OrganizationInvitations", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "OrgSample_Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Metadata = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ArchivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgSample_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrgSample_OrganizationMemberships",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgSample_OrganizationMemberships", x => new { x.OrganizationId, x.UserId });
                    table.ForeignKey(
                        name: "FK_OrgSample_OrganizationMemberships_OrgSample_Organizations_O~",
                        column: x => x.OrganizationId,
                        principalTable: "OrgSample_Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrgSample_OrganizationRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsSystemRole = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgSample_OrganizationRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrgSample_OrganizationRoles_OrgSample_Organizations_Organiz~",
                        column: x => x.OrganizationId,
                        principalTable: "OrgSample_Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrgSample_OrganizationRoleAssignments",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgSample_OrganizationRoleAssignments", x => new { x.OrganizationId, x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_OrgSample_OrganizationRoleAssignments_OrgSample_Organizatio~",
                        column: x => x.OrganizationId,
                        principalTable: "OrgSample_Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrgSample_OrganizationRoleAssignments_OrgSample_Organizati~1",
                        column: x => x.RoleId,
                        principalTable: "OrgSample_OrganizationRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrgSample_OrganizationRoleAssignments_OrgSample_Organizati~2",
                        columns: x => new { x.OrganizationId, x.UserId },
                        principalTable: "OrgSample_OrganizationMemberships",
                        principalColumns: new[] { "OrganizationId", "UserId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrgSample_OrganizationRolePermissions",
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
                    table.PrimaryKey("PK_OrgSample_OrganizationRolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrgSample_OrganizationRolePermissions_OrgSample_Organizatio~",
                        column: x => x.RoleId,
                        principalTable: "OrgSample_OrganizationRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_OrganizationInvitations_Email",
                table: "OrgSample_OrganizationInvitations",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_OrganizationInvitations_OrganizationId",
                table: "OrgSample_OrganizationInvitations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_OrganizationInvitations_UsedAtUtc",
                table: "OrgSample_OrganizationInvitations",
                column: "UsedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_OrganizationMemberships_Organization_Created",
                table: "OrgSample_OrganizationMemberships",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_OrganizationMemberships_Organization_User",
                table: "OrgSample_OrganizationMemberships",
                columns: new[] { "OrganizationId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_OrganizationMemberships_User_Tenant",
                table: "OrgSample_OrganizationMemberships",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_OrganizationRoleAssignments_Role",
                table: "OrgSample_OrganizationRoleAssignments",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_OrganizationRoleAssignments_User_Tenant",
                table: "OrgSample_OrganizationRoleAssignments",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_OrganizationRolePermissions_Organization_Role",
                table: "OrgSample_OrganizationRolePermissions",
                columns: new[] { "OrganizationId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_OrganizationRolePermissions_Role_Permission",
                table: "OrgSample_OrganizationRolePermissions",
                columns: new[] { "RoleId", "PermissionId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_OrganizationRolePermissions_Tenant_Role",
                table: "OrgSample_OrganizationRolePermissions",
                columns: new[] { "TenantId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_OrganizationRoles_OrganizationId",
                table: "OrgSample_OrganizationRoles",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_OrganizationRoles_Tenant_Organization_Name",
                table: "OrgSample_OrganizationRoles",
                columns: new[] { "TenantId", "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_Organizations_Tenant_DisplayName",
                table: "OrgSample_Organizations",
                columns: new[] { "TenantId", "DisplayName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_Organizations_Tenant_Slug",
                table: "OrgSample_Organizations",
                columns: new[] { "TenantId", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrgSample_OrganizationInvitations");

            migrationBuilder.DropTable(
                name: "OrgSample_OrganizationRoleAssignments");

            migrationBuilder.DropTable(
                name: "OrgSample_OrganizationRolePermissions");

            migrationBuilder.DropTable(
                name: "OrgSample_OrganizationMemberships");

            migrationBuilder.DropTable(
                name: "OrgSample_OrganizationRoles");

            migrationBuilder.DropTable(
                name: "OrgSample_Organizations");
        }
    }
}
