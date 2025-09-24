using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Base.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOpenIddictEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationId",
                table: "Identity_OpenIddictTokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AuthorizationId",
                table: "Identity_OpenIddictTokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyToken",
                table: "Identity_OpenIddictTokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreationDate",
                table: "Identity_OpenIddictTokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpirationDate",
                table: "Identity_OpenIddictTokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Payload",
                table: "Identity_OpenIddictTokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Properties",
                table: "Identity_OpenIddictTokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RedemptionDate",
                table: "Identity_OpenIddictTokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceId",
                table: "Identity_OpenIddictTokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Identity_OpenIddictTokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "Identity_OpenIddictTokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Identity_OpenIddictTokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyToken",
                table: "Identity_OpenIddictScopes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Identity_OpenIddictScopes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Descriptions",
                table: "Identity_OpenIddictScopes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Identity_OpenIddictScopes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayNames",
                table: "Identity_OpenIddictScopes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Identity_OpenIddictScopes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Properties",
                table: "Identity_OpenIddictScopes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Resources",
                table: "Identity_OpenIddictScopes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationId",
                table: "Identity_OpenIddictAuthorizations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyToken",
                table: "Identity_OpenIddictAuthorizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreationDate",
                table: "Identity_OpenIddictAuthorizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Properties",
                table: "Identity_OpenIddictAuthorizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Scopes",
                table: "Identity_OpenIddictAuthorizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Identity_OpenIddictAuthorizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "Identity_OpenIddictAuthorizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Identity_OpenIddictAuthorizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicationType",
                table: "Identity_OpenIddictApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientId",
                table: "Identity_OpenIddictApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientSecret",
                table: "Identity_OpenIddictApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientType",
                table: "Identity_OpenIddictApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyToken",
                table: "Identity_OpenIddictApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsentType",
                table: "Identity_OpenIddictApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Identity_OpenIddictApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayNames",
                table: "Identity_OpenIddictApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JsonWebKeySet",
                table: "Identity_OpenIddictApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Permissions",
                table: "Identity_OpenIddictApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostLogoutRedirectUris",
                table: "Identity_OpenIddictApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Properties",
                table: "Identity_OpenIddictApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RedirectUris",
                table: "Identity_OpenIddictApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Requirements",
                table: "Identity_OpenIddictApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Settings",
                table: "Identity_OpenIddictApplications",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Identity_OpenIddictTokens_ApplicationId",
                table: "Identity_OpenIddictTokens",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_Identity_OpenIddictTokens_AuthorizationId",
                table: "Identity_OpenIddictTokens",
                column: "AuthorizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Identity_OpenIddictAuthorizations_ApplicationId",
                table: "Identity_OpenIddictAuthorizations",
                column: "ApplicationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Identity_OpenIddictAuthorizations_Identity_OpenIddictApplic~",
                table: "Identity_OpenIddictAuthorizations",
                column: "ApplicationId",
                principalTable: "Identity_OpenIddictApplications",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Identity_OpenIddictTokens_Identity_OpenIddictApplications_A~",
                table: "Identity_OpenIddictTokens",
                column: "ApplicationId",
                principalTable: "Identity_OpenIddictApplications",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Identity_OpenIddictTokens_Identity_OpenIddictAuthorizations~",
                table: "Identity_OpenIddictTokens",
                column: "AuthorizationId",
                principalTable: "Identity_OpenIddictAuthorizations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Identity_OpenIddictAuthorizations_Identity_OpenIddictApplic~",
                table: "Identity_OpenIddictAuthorizations");

            migrationBuilder.DropForeignKey(
                name: "FK_Identity_OpenIddictTokens_Identity_OpenIddictApplications_A~",
                table: "Identity_OpenIddictTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_Identity_OpenIddictTokens_Identity_OpenIddictAuthorizations~",
                table: "Identity_OpenIddictTokens");

            migrationBuilder.DropIndex(
                name: "IX_Identity_OpenIddictTokens_ApplicationId",
                table: "Identity_OpenIddictTokens");

            migrationBuilder.DropIndex(
                name: "IX_Identity_OpenIddictTokens_AuthorizationId",
                table: "Identity_OpenIddictTokens");

            migrationBuilder.DropIndex(
                name: "IX_Identity_OpenIddictAuthorizations_ApplicationId",
                table: "Identity_OpenIddictAuthorizations");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                table: "Identity_OpenIddictTokens");

            migrationBuilder.DropColumn(
                name: "AuthorizationId",
                table: "Identity_OpenIddictTokens");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "Identity_OpenIddictTokens");

            migrationBuilder.DropColumn(
                name: "CreationDate",
                table: "Identity_OpenIddictTokens");

            migrationBuilder.DropColumn(
                name: "ExpirationDate",
                table: "Identity_OpenIddictTokens");

            migrationBuilder.DropColumn(
                name: "Payload",
                table: "Identity_OpenIddictTokens");

            migrationBuilder.DropColumn(
                name: "Properties",
                table: "Identity_OpenIddictTokens");

            migrationBuilder.DropColumn(
                name: "RedemptionDate",
                table: "Identity_OpenIddictTokens");

            migrationBuilder.DropColumn(
                name: "ReferenceId",
                table: "Identity_OpenIddictTokens");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Identity_OpenIddictTokens");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "Identity_OpenIddictTokens");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Identity_OpenIddictTokens");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "Identity_OpenIddictScopes");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Identity_OpenIddictScopes");

            migrationBuilder.DropColumn(
                name: "Descriptions",
                table: "Identity_OpenIddictScopes");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Identity_OpenIddictScopes");

            migrationBuilder.DropColumn(
                name: "DisplayNames",
                table: "Identity_OpenIddictScopes");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Identity_OpenIddictScopes");

            migrationBuilder.DropColumn(
                name: "Properties",
                table: "Identity_OpenIddictScopes");

            migrationBuilder.DropColumn(
                name: "Resources",
                table: "Identity_OpenIddictScopes");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                table: "Identity_OpenIddictAuthorizations");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "Identity_OpenIddictAuthorizations");

            migrationBuilder.DropColumn(
                name: "CreationDate",
                table: "Identity_OpenIddictAuthorizations");

            migrationBuilder.DropColumn(
                name: "Properties",
                table: "Identity_OpenIddictAuthorizations");

            migrationBuilder.DropColumn(
                name: "Scopes",
                table: "Identity_OpenIddictAuthorizations");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Identity_OpenIddictAuthorizations");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "Identity_OpenIddictAuthorizations");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Identity_OpenIddictAuthorizations");

            migrationBuilder.DropColumn(
                name: "ApplicationType",
                table: "Identity_OpenIddictApplications");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "Identity_OpenIddictApplications");

            migrationBuilder.DropColumn(
                name: "ClientSecret",
                table: "Identity_OpenIddictApplications");

            migrationBuilder.DropColumn(
                name: "ClientType",
                table: "Identity_OpenIddictApplications");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "Identity_OpenIddictApplications");

            migrationBuilder.DropColumn(
                name: "ConsentType",
                table: "Identity_OpenIddictApplications");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Identity_OpenIddictApplications");

            migrationBuilder.DropColumn(
                name: "DisplayNames",
                table: "Identity_OpenIddictApplications");

            migrationBuilder.DropColumn(
                name: "JsonWebKeySet",
                table: "Identity_OpenIddictApplications");

            migrationBuilder.DropColumn(
                name: "Permissions",
                table: "Identity_OpenIddictApplications");

            migrationBuilder.DropColumn(
                name: "PostLogoutRedirectUris",
                table: "Identity_OpenIddictApplications");

            migrationBuilder.DropColumn(
                name: "Properties",
                table: "Identity_OpenIddictApplications");

            migrationBuilder.DropColumn(
                name: "RedirectUris",
                table: "Identity_OpenIddictApplications");

            migrationBuilder.DropColumn(
                name: "Requirements",
                table: "Identity_OpenIddictApplications");

            migrationBuilder.DropColumn(
                name: "Settings",
                table: "Identity_OpenIddictApplications");
        }
    }
}
