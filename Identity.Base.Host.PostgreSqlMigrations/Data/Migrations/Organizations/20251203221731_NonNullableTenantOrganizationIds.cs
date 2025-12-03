using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Base.Host.PostgreSqlMigrations.Data.Migrations.Organizations
{
    /// <inheritdoc />
    public partial class NonNullableTenantOrganizationIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First update existing NULL values to Guid.Empty before changing column nullability
            migrationBuilder.Sql("UPDATE \"Host_Organizations\" SET \"TenantId\" = '00000000-0000-0000-0000-000000000000' WHERE \"TenantId\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"Host_OrganizationRoles\" SET \"TenantId\" = '00000000-0000-0000-0000-000000000000' WHERE \"TenantId\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"Host_OrganizationRoles\" SET \"OrganizationId\" = '00000000-0000-0000-0000-000000000000' WHERE \"OrganizationId\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"Host_OrganizationRolePermissions\" SET \"TenantId\" = '00000000-0000-0000-0000-000000000000' WHERE \"TenantId\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"Host_OrganizationRolePermissions\" SET \"OrganizationId\" = '00000000-0000-0000-0000-000000000000' WHERE \"OrganizationId\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"Host_OrganizationRoleAssignments\" SET \"TenantId\" = '00000000-0000-0000-0000-000000000000' WHERE \"TenantId\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"Host_OrganizationMemberships\" SET \"TenantId\" = '00000000-0000-0000-0000-000000000000' WHERE \"TenantId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Host_Organizations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Host_OrganizationRoles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                table: "Host_OrganizationRoles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Host_OrganizationRolePermissions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                table: "Host_OrganizationRolePermissions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Host_OrganizationRoleAssignments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Host_OrganizationMemberships",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Host_Organizations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Host_OrganizationRoles",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                table: "Host_OrganizationRoles",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Host_OrganizationRolePermissions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                table: "Host_OrganizationRolePermissions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Host_OrganizationRoleAssignments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Host_OrganizationMemberships",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }
    }
}
