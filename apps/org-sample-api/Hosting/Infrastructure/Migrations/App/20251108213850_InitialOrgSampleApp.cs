using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OrgSampleApi.Hosting.Infrastructure.Migrations.App
{
    /// <inheritdoc />
    public partial class InitialOrgSampleApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrgSample_OpenIddictApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationType = table.Column<string>(type: "text", nullable: true),
                    ClientId = table.Column<string>(type: "text", nullable: true),
                    ClientSecret = table.Column<string>(type: "text", nullable: true),
                    ClientType = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "text", nullable: true),
                    ConsentType = table.Column<string>(type: "text", nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    DisplayNames = table.Column<string>(type: "text", nullable: true),
                    JsonWebKeySet = table.Column<string>(type: "text", nullable: true),
                    Permissions = table.Column<string>(type: "text", nullable: true),
                    PostLogoutRedirectUris = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    RedirectUris = table.Column<string>(type: "text", nullable: true),
                    Requirements = table.Column<string>(type: "text", nullable: true),
                    Settings = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgSample_OpenIddictApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrgSample_OpenIddictScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Descriptions = table.Column<string>(type: "text", nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    DisplayNames = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    Resources = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgSample_OpenIddictScopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrgSample_Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgSample_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrgSample_Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ProfileMetadata = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgSample_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrgSample_OpenIddictAuthorizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "text", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    Scopes = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: true),
                    Subject = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgSample_OpenIddictAuthorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrgSample_OpenIddictAuthorizations_OrgSample_OpenIddictAppl~",
                        column: x => x.ApplicationId,
                        principalTable: "OrgSample_OpenIddictApplications",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OrgSample_RoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgSample_RoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrgSample_RoleClaims_OrgSample_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "OrgSample_Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrgSample_UserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgSample_UserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrgSample_UserClaims_OrgSample_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "OrgSample_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrgSample_UserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgSample_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_OrgSample_UserLogins_OrgSample_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "OrgSample_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrgSample_UserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgSample_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_OrgSample_UserRoles_OrgSample_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "OrgSample_Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrgSample_UserRoles_OrgSample_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "OrgSample_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrgSample_UserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgSample_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_OrgSample_UserTokens_OrgSample_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "OrgSample_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrgSample_OpenIddictTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "text", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    RedemptionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReferenceId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: true),
                    Subject = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgSample_OpenIddictTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrgSample_OpenIddictTokens_OrgSample_OpenIddictApplications~",
                        column: x => x.ApplicationId,
                        principalTable: "OrgSample_OpenIddictApplications",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OrgSample_OpenIddictTokens_OrgSample_OpenIddictAuthorizatio~",
                        column: x => x.AuthorizationId,
                        principalTable: "OrgSample_OpenIddictAuthorizations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_OpenIddictAuthorizations_ApplicationId",
                table: "OrgSample_OpenIddictAuthorizations",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_OpenIddictTokens_ApplicationId",
                table: "OrgSample_OpenIddictTokens",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_OpenIddictTokens_AuthorizationId",
                table: "OrgSample_OpenIddictTokens",
                column: "AuthorizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_RoleClaims_RoleId",
                table: "OrgSample_RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_Roles_NormalizedName",
                table: "OrgSample_Roles",
                column: "NormalizedName",
                unique: true,
                filter: "\"NormalizedName\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_UserClaims_UserId",
                table: "OrgSample_UserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_UserLogins_UserId",
                table: "OrgSample_UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_UserRoles_RoleId",
                table: "OrgSample_UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_Users_CreatedAt",
                table: "OrgSample_Users",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_Users_Email",
                table: "OrgSample_Users",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_Users_NormalizedEmail",
                table: "OrgSample_Users",
                column: "NormalizedEmail",
                unique: true,
                filter: "\"NormalizedEmail\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrgSample_Users_NormalizedUserName",
                table: "OrgSample_Users",
                column: "NormalizedUserName",
                unique: true,
                filter: "\"NormalizedUserName\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrgSample_OpenIddictScopes");

            migrationBuilder.DropTable(
                name: "OrgSample_OpenIddictTokens");

            migrationBuilder.DropTable(
                name: "OrgSample_RoleClaims");

            migrationBuilder.DropTable(
                name: "OrgSample_UserClaims");

            migrationBuilder.DropTable(
                name: "OrgSample_UserLogins");

            migrationBuilder.DropTable(
                name: "OrgSample_UserRoles");

            migrationBuilder.DropTable(
                name: "OrgSample_UserTokens");

            migrationBuilder.DropTable(
                name: "OrgSample_OpenIddictAuthorizations");

            migrationBuilder.DropTable(
                name: "OrgSample_Roles");

            migrationBuilder.DropTable(
                name: "OrgSample_Users");

            migrationBuilder.DropTable(
                name: "OrgSample_OpenIddictApplications");
        }
    }
}
