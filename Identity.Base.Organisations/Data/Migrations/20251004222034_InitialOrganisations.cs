using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Base.Organisations.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialOrganisations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Identity_Organisations",
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
                    table.PrimaryKey("PK_Identity_Organisations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Identity_OrganisationMemberships",
                columns: table => new
                {
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Identity_OrganisationMemberships", x => new { x.OrganisationId, x.UserId });
                    table.ForeignKey(
                        name: "FK_Identity_OrganisationMemberships_Identity_Organisations_Org~",
                        column: x => x.OrganisationId,
                        principalTable: "Identity_Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Identity_OrganisationRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsSystemRole = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Identity_OrganisationRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Identity_OrganisationRoles_Identity_Organisations_Organizat~",
                        column: x => x.OrganisationId,
                        principalTable: "Identity_Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Identity_OrganisationRoleAssignments",
                columns: table => new
                {
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Identity_OrganisationRoleAssignments", x => new { x.OrganisationId, x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_Identity_OrganisationRoleAssignments_Identity_OrganisationM~",
                        columns: x => new { x.OrganisationId, x.UserId },
                        principalTable: "Identity_OrganisationMemberships",
                        principalColumns: new[] { "OrganisationId", "UserId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Identity_OrganisationRoleAssignments_Identity_OrganisationR~",
                        column: x => x.RoleId,
                        principalTable: "Identity_OrganisationRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Identity_OrganisationRoleAssignments_Identity_Organisations~",
                        column: x => x.OrganisationId,
                        principalTable: "Identity_Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationMemberships_User_Tenant",
                table: "Identity_OrganisationMemberships",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationRoleAssignments_Role",
                table: "Identity_OrganisationRoleAssignments",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationRoleAssignments_User_Tenant",
                table: "Identity_OrganisationRoleAssignments",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Identity_OrganisationRoles_OrganisationId",
                table: "Identity_OrganisationRoles",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationRoles_Tenant_Organisation_Name",
                table: "Identity_OrganisationRoles",
                columns: new[] { "TenantId", "OrganisationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organisations_Tenant_DisplayName",
                table: "Identity_Organisations",
                columns: new[] { "TenantId", "DisplayName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organisations_Tenant_Slug",
                table: "Identity_Organisations",
                columns: new[] { "TenantId", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Identity_OrganisationRoleAssignments");

            migrationBuilder.DropTable(
                name: "Identity_OrganisationMemberships");

            migrationBuilder.DropTable(
                name: "Identity_OrganisationRoles");

            migrationBuilder.DropTable(
                name: "Identity_Organisations");
        }
    }
}
