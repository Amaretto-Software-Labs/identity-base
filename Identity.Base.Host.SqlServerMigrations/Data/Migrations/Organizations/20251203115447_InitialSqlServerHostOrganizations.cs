using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Base.Host.SqlServerMigrations.Data.Migrations.Organizations
{
    /// <inheritdoc />
    public partial class InitialSqlServerHostOrganizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Host_OrganizationInvitations",
                columns: table => new
                {
                    Code = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationSlug = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OrganizationName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RoleIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UsedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Host_OrganizationInvitations", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Host_Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Slug = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ArchivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Host_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Host_OrganizationMemberships",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Host_OrganizationMemberships", x => new { x.OrganizationId, x.UserId });
                    table.ForeignKey(
                        name: "FK_Host_OrganizationMemberships_Host_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Host_Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Host_OrganizationRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsSystemRole = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Host_OrganizationRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Host_OrganizationRoles_Host_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Host_Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Host_OrganizationRoleAssignments",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Host_OrganizationRoleAssignments", x => new { x.OrganizationId, x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_Host_OrganizationRoleAssignments_Host_OrganizationMemberships_OrganizationId_UserId",
                        columns: x => new { x.OrganizationId, x.UserId },
                        principalTable: "Host_OrganizationMemberships",
                        principalColumns: new[] { "OrganizationId", "UserId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Host_OrganizationRoleAssignments_Host_OrganizationRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Host_OrganizationRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Host_OrganizationRoleAssignments_Host_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Host_Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Host_OrganizationRolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Host_OrganizationRolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Host_OrganizationRolePermissions_Host_OrganizationRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Host_OrganizationRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Host_OrganizationInvitations_Email",
                table: "Host_OrganizationInvitations",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Host_OrganizationInvitations_OrganizationId",
                table: "Host_OrganizationInvitations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Host_OrganizationInvitations_UsedAtUtc",
                table: "Host_OrganizationInvitations",
                column: "UsedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Host_OrganizationMemberships_Organization_Created",
                table: "Host_OrganizationMemberships",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Host_OrganizationMemberships_Organization_User",
                table: "Host_OrganizationMemberships",
                columns: new[] { "OrganizationId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Host_OrganizationMemberships_User_Tenant",
                table: "Host_OrganizationMemberships",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Host_OrganizationRoleAssignments_Role",
                table: "Host_OrganizationRoleAssignments",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Host_OrganizationRoleAssignments_User_Tenant",
                table: "Host_OrganizationRoleAssignments",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Host_OrganizationRolePermissions_Organization_Role",
                table: "Host_OrganizationRolePermissions",
                columns: new[] { "OrganizationId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_Host_OrganizationRolePermissions_Role_Permission",
                table: "Host_OrganizationRolePermissions",
                columns: new[] { "RoleId", "PermissionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Host_OrganizationRolePermissions_Tenant_Role",
                table: "Host_OrganizationRolePermissions",
                columns: new[] { "TenantId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_Host_OrganizationRoles_OrganizationId",
                table: "Host_OrganizationRoles",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Host_OrganizationRoles_Tenant_Organization_Name",
                table: "Host_OrganizationRoles",
                columns: new[] { "TenantId", "OrganizationId", "Name" },
                unique: true,
                filter: "[TenantId] IS NOT NULL AND [OrganizationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Host_Organizations_Tenant_DisplayName",
                table: "Host_Organizations",
                columns: new[] { "TenantId", "DisplayName" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Host_Organizations_Tenant_Slug",
                table: "Host_Organizations",
                columns: new[] { "TenantId", "Slug" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Host_OrganizationInvitations");

            migrationBuilder.DropTable(
                name: "Host_OrganizationRoleAssignments");

            migrationBuilder.DropTable(
                name: "Host_OrganizationRolePermissions");

            migrationBuilder.DropTable(
                name: "Host_OrganizationMemberships");

            migrationBuilder.DropTable(
                name: "Host_OrganizationRoles");

            migrationBuilder.DropTable(
                name: "Host_Organizations");
        }
    }
}
