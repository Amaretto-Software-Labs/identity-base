using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Base.Host.PostgreSqlMigrations.Data.Migrations.Organizations
{
    /// <inheritdoc />
    public partial class EnforceUniqueOrganizationInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT "Code",
                           ROW_NUMBER() OVER (
                               PARTITION BY "OrganizationId", "Email"
                               ORDER BY
                                   CASE
                                       WHEN "UsedAtUtc" IS NULL AND "ExpiresAtUtc" > CURRENT_TIMESTAMP THEN 0
                                       ELSE 1
                                   END,
                                   "CreatedAtUtc" DESC,
                                   "Code"
                           ) AS duplicate_rank
                    FROM "Host_OrganizationInvitations"
                )
                DELETE FROM "Host_OrganizationInvitations" AS invitation
                USING ranked
                WHERE invitation."Code" = ranked."Code"
                  AND ranked.duplicate_rank > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Host_OrganizationInvitations_OrganizationId_Email",
                table: "Host_OrganizationInvitations",
                columns: new[] { "OrganizationId", "Email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Host_OrganizationInvitations_OrganizationId_Email",
                table: "Host_OrganizationInvitations");
        }
    }
}
