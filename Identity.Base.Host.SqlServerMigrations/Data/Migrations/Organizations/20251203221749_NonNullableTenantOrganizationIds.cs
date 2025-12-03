using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Base.Host.SqlServerMigrations.Data.Migrations.Organizations
{
    /// <inheritdoc />
    public partial class NonNullableTenantOrganizationIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First update existing NULL values to Guid.Empty before changing column nullability
            migrationBuilder.Sql("UPDATE [Host_Organizations] SET [TenantId] = '00000000-0000-0000-0000-000000000000' WHERE [TenantId] IS NULL;");
            migrationBuilder.Sql("UPDATE [Host_OrganizationRoles] SET [TenantId] = '00000000-0000-0000-0000-000000000000' WHERE [TenantId] IS NULL;");
            migrationBuilder.Sql("UPDATE [Host_OrganizationRoles] SET [OrganizationId] = '00000000-0000-0000-0000-000000000000' WHERE [OrganizationId] IS NULL;");
            migrationBuilder.Sql("UPDATE [Host_OrganizationRolePermissions] SET [TenantId] = '00000000-0000-0000-0000-000000000000' WHERE [TenantId] IS NULL;");
            migrationBuilder.Sql("UPDATE [Host_OrganizationRolePermissions] SET [OrganizationId] = '00000000-0000-0000-0000-000000000000' WHERE [OrganizationId] IS NULL;");
            migrationBuilder.Sql("UPDATE [Host_OrganizationRoleAssignments] SET [TenantId] = '00000000-0000-0000-0000-000000000000' WHERE [TenantId] IS NULL;");
            migrationBuilder.Sql("UPDATE [Host_OrganizationMemberships] SET [TenantId] = '00000000-0000-0000-0000-000000000000' WHERE [TenantId] IS NULL;");

            migrationBuilder.DropIndex(
                name: "IX_Host_Organizations_Tenant_DisplayName",
                table: "Host_Organizations");

            migrationBuilder.DropIndex(
                name: "IX_Host_Organizations_Tenant_Slug",
                table: "Host_Organizations");

            migrationBuilder.DropIndex(
                name: "IX_Host_OrganizationRoles_Tenant_Organization_Name",
                table: "Host_OrganizationRoles");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Host_Organizations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Host_OrganizationRoles",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                table: "Host_OrganizationRoles",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Host_OrganizationRolePermissions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                table: "Host_OrganizationRolePermissions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Host_OrganizationRoleAssignments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Host_OrganizationMemberships",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Host_Organizations_Tenant_DisplayName",
                table: "Host_Organizations",
                columns: new[] { "TenantId", "DisplayName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Host_Organizations_Tenant_Slug",
                table: "Host_Organizations",
                columns: new[] { "TenantId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Host_OrganizationRoles_Tenant_Organization_Name",
                table: "Host_OrganizationRoles",
                columns: new[] { "TenantId", "OrganizationId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Host_Organizations_Tenant_DisplayName",
                table: "Host_Organizations");

            migrationBuilder.DropIndex(
                name: "IX_Host_Organizations_Tenant_Slug",
                table: "Host_Organizations");

            migrationBuilder.DropIndex(
                name: "IX_Host_OrganizationRoles_Tenant_Organization_Name",
                table: "Host_OrganizationRoles");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Host_Organizations",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Host_OrganizationRoles",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                table: "Host_OrganizationRoles",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Host_OrganizationRolePermissions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                table: "Host_OrganizationRolePermissions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Host_OrganizationRoleAssignments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Host_OrganizationMemberships",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

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

            migrationBuilder.CreateIndex(
                name: "IX_Host_OrganizationRoles_Tenant_Organization_Name",
                table: "Host_OrganizationRoles",
                columns: new[] { "TenantId", "OrganizationId", "Name" },
                unique: true,
                filter: "[TenantId] IS NOT NULL AND [OrganizationId] IS NOT NULL");
        }
    }
}
