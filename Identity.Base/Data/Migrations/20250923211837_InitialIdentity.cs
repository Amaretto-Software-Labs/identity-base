using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Identity.Base.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Identity_OpenIddictApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Identity_OpenIddictApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Identity_OpenIddictAuthorizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Identity_OpenIddictAuthorizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Identity_OpenIddictScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Identity_OpenIddictScopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Identity_OpenIddictTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Identity_OpenIddictTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Identity_Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Identity_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Identity_Users",
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
                    table.PrimaryKey("PK_Identity_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Identity_RoleClaims",
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
                    table.PrimaryKey("PK_Identity_RoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Identity_RoleClaims_Identity_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Identity_Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Identity_UserClaims",
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
                    table.PrimaryKey("PK_Identity_UserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Identity_UserClaims_Identity_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Identity_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Identity_UserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Identity_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_Identity_UserLogins_Identity_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Identity_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Identity_UserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Identity_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_Identity_UserRoles_Identity_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Identity_Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Identity_UserRoles_Identity_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Identity_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Identity_UserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Identity_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_Identity_UserTokens_Identity_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Identity_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Identity_RoleClaims_RoleId",
                table: "Identity_RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Identity_Roles_NormalizedName",
                table: "Identity_Roles",
                column: "NormalizedName",
                unique: true,
                filter: "\"NormalizedName\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Identity_UserClaims_UserId",
                table: "Identity_UserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Identity_UserLogins_UserId",
                table: "Identity_UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Identity_UserRoles_RoleId",
                table: "Identity_UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Identity_Users_Email",
                table: "Identity_Users",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Identity_Users_NormalizedEmail",
                table: "Identity_Users",
                column: "NormalizedEmail",
                unique: true,
                filter: "\"NormalizedEmail\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Identity_Users_NormalizedUserName",
                table: "Identity_Users",
                column: "NormalizedUserName",
                unique: true,
                filter: "\"NormalizedUserName\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Identity_OpenIddictApplications");

            migrationBuilder.DropTable(
                name: "Identity_OpenIddictAuthorizations");

            migrationBuilder.DropTable(
                name: "Identity_OpenIddictScopes");

            migrationBuilder.DropTable(
                name: "Identity_OpenIddictTokens");

            migrationBuilder.DropTable(
                name: "Identity_RoleClaims");

            migrationBuilder.DropTable(
                name: "Identity_UserClaims");

            migrationBuilder.DropTable(
                name: "Identity_UserLogins");

            migrationBuilder.DropTable(
                name: "Identity_UserRoles");

            migrationBuilder.DropTable(
                name: "Identity_UserTokens");

            migrationBuilder.DropTable(
                name: "Identity_Roles");

            migrationBuilder.DropTable(
                name: "Identity_Users");
        }
    }
}
