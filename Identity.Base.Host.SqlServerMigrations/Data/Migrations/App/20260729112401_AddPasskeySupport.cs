using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Base.Host.SqlServerMigrations.Data.Migrations.App
{
    /// <inheritdoc />
    public partial class AddPasskeySupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Host_PasskeyRecoveryDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConfirmationTokenHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EmailConfirmedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Host_PasskeyRecoveryDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Host_PasskeyRecoveryDrafts_Host_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Host_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Host_PasskeyRegistrationDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProfileMetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ConfirmationTokenHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EmailConfirmedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Host_PasskeyRegistrationDrafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Host_UserPasskeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CredentialId = table.Column<byte[]>(type: "varbinary(1024)", maxLength: 1024, nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Host_UserPasskeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Host_UserPasskeys_Host_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Host_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Host_PasskeyRecoveryDrafts_ExpiresAt",
                table: "Host_PasskeyRecoveryDrafts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Host_PasskeyRecoveryDrafts_UserId",
                table: "Host_PasskeyRecoveryDrafts",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Host_PasskeyRegistrationDrafts_ExpiresAt",
                table: "Host_PasskeyRegistrationDrafts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Host_PasskeyRegistrationDrafts_NormalizedEmail",
                table: "Host_PasskeyRegistrationDrafts",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Host_UserPasskeys_CredentialId",
                table: "Host_UserPasskeys",
                column: "CredentialId",
                unique: true,
                filter: "[CredentialId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Host_UserPasskeys_UserId",
                table: "Host_UserPasskeys",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Host_PasskeyRecoveryDrafts");

            migrationBuilder.DropTable(
                name: "Host_PasskeyRegistrationDrafts");

            migrationBuilder.DropTable(
                name: "Host_UserPasskeys");
        }
    }
}
