using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Base.Organizations.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
DO $$
BEGIN
    IF to_regclass(format('%I.%I', current_schema(), 'Identity_OrganizationInvitations')) IS NULL THEN
        CREATE TABLE "Identity_OrganizationInvitations" (
            "Code" uuid NOT NULL,
            "OrganizationId" uuid NOT NULL,
            "OrganizationSlug" character varying(128) NOT NULL,
            "OrganizationName" character varying(256) NOT NULL,
            "Email" character varying(256) NOT NULL,
            "RoleIds" jsonb NOT NULL,
            "CreatedBy" uuid NULL,
            "CreatedAtUtc" timestamptz NOT NULL,
            "ExpiresAtUtc" timestamptz NOT NULL,
            "UsedAtUtc" timestamptz NULL,
            "UsedByUserId" uuid NULL,
            CONSTRAINT "PK_Identity_OrganizationInvitations" PRIMARY KEY ("Code")
        );
    END IF;

    ALTER TABLE "Identity_OrganizationInvitations"
        ADD COLUMN IF NOT EXISTS "UsedAtUtc" timestamptz NULL,
        ADD COLUMN IF NOT EXISTS "UsedByUserId" uuid NULL,
        ADD COLUMN IF NOT EXISTS "CreatedBy" uuid NULL,
        ADD COLUMN IF NOT EXISTS "RoleIds" jsonb NOT NULL DEFAULT '[]'::jsonb,
        ADD COLUMN IF NOT EXISTS "OrganizationSlug" character varying(128) NOT NULL DEFAULT ''::text,
        ADD COLUMN IF NOT EXISTS "OrganizationName" character varying(256) NOT NULL DEFAULT ''::text,
        ADD COLUMN IF NOT EXISTS "Email" character varying(256) NOT NULL DEFAULT ''::text;

    ALTER TABLE "Identity_OrganizationInvitations"
        ALTER COLUMN "OrganizationSlug" TYPE character varying(128),
        ALTER COLUMN "OrganizationName" TYPE character varying(256),
        ALTER COLUMN "Email" TYPE character varying(256);

    ALTER TABLE "Identity_OrganizationInvitations"
        ALTER COLUMN "RoleIds" DROP DEFAULT,
        ALTER COLUMN "OrganizationSlug" DROP DEFAULT,
        ALTER COLUMN "OrganizationName" DROP DEFAULT,
        ALTER COLUMN "Email" DROP DEFAULT;

    CREATE INDEX IF NOT EXISTS "IX_Identity_OrganizationInvitations_Email"
        ON "Identity_OrganizationInvitations" ("Email");

    CREATE INDEX IF NOT EXISTS "IX_Identity_OrganizationInvitations_OrganizationId"
        ON "Identity_OrganizationInvitations" ("OrganizationId");

    CREATE INDEX IF NOT EXISTS "IX_Identity_OrganizationInvitations_UsedAtUtc"
        ON "Identity_OrganizationInvitations" ("UsedAtUtc");
END
$$;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"Identity_OrganizationInvitations\" CASCADE;");
        }
    }
}
