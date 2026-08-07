using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateNumberCountersAndFixCertificateNumberIndexScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_toolbox_talk_certificates_number",
                schema: "toolbox_talks",
                table: "ToolboxTalkCertificates");

            migrationBuilder.CreateTable(
                name: "CertificateNumberCounters",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    LastNumber = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateNumberCounters", x => x.Id);
                });

            // Seed one counter row per {TenantId, Prefix, Year} scope already represented in
            // ToolboxTalkCertificates, so the next allocation for an established tenant continues
            // at N+1 instead of restarting at 1 (which would collide with the tenant's own
            // existing {prefix}-{year}-000001 row via the unique index created below).
            //
            // Prefix/Year are parsed OUT of the existing CertificateNumber string rather than
            // re-derived independently, and that is deliberately correct here, not a shortcut:
            // CertificateGenerationService.GenerateCertificateNumber builds that exact string as
            // $"{prefix}-{year}-{sequence:D6}" from the tenant's current prefix setting and
            // DateTime.UtcNow.Year at the moment of generation - so the Prefix/Year substrings
            // embedded in every historical row ARE the literal values the allocator used to look
            // up its scope at that time. Reading them back out reconstructs the same scope key the
            // allocator will look up again today, as long as the tenant's prefix setting and the
            // calendar year haven't changed since - the common case for an "established tenant"
            // this migration needs to protect. If either has changed, the old scope simply becomes
            // inert (no future allocation will ever look it up again) while the new scope
            // correctly starts at 1, which is correct: no certificates exist under the new
            // prefix/year yet.
            //
            // regexp_matches (not the singular regexp_match) is used specifically because it is a
            // set-returning function: a non-matching CertificateNumber (unexpected legacy format,
            // manually-entered row, anything not shaped like "PREFIX-YYYY-NNNNNN") produces zero
            // rows from the LATERAL join and is silently excluded rather than causing a NULL
            // Prefix/Year/cast error. The additional length() guards keep every cast safe: Prefix
            // is capped at 20 chars to match CertificateNumberCounter.Prefix's HasMaxLength(20)
            // (an over-long parse would otherwise fail the INSERT), and Seq is capped at 9 digits
            // to keep the ::int cast for MAX() within int32 range - both simply drop the offending
            // row rather than throwing, consistent with "malformed rows are skipped, never cast".
            //
            // Soft-deleted certificates are deliberately INCLUDED in the MAX (no IsDeleted filter
            // below): the unique index on ToolboxTalkCertificates being replaced below is itself
            // not IsDeleted-filtered (see ToolboxTalkCertificateConfiguration.cs), so a soft-deleted
            // certificate's number is still live in that index and must not be reissued.
            //
            // GROUP BY {TenantId, Prefix, Year} guarantees at most one row per scope, matching the
            // unique index created on CertificateNumberCounters below - if that invariant were ever
            // violated by a parsing bug, the index creation immediately after this INSERT would
            // fail loudly rather than silently admitting duplicate counter rows.
            migrationBuilder.Sql(
                """
                INSERT INTO toolbox_talks."CertificateNumberCounters"
                    ("Id", "TenantId", "Prefix", "Year", "LastNumber", "CreatedAt", "CreatedBy", "IsDeleted")
                SELECT
                    gen_random_uuid(),
                    parsed."TenantId",
                    parsed."Prefix",
                    parsed."Year",
                    MAX(parsed."Seq"::int),
                    now(),
                    'system-migration',
                    false
                FROM (
                    SELECT
                        c."TenantId" AS "TenantId",
                        m[1] AS "Prefix",
                        (m[2])::int AS "Year",
                        m[3] AS "Seq"
                    FROM toolbox_talks."ToolboxTalkCertificates" c,
                         LATERAL regexp_matches(c."CertificateNumber", '^(.+)-(\d{4})-(\d+)$') AS m
                    WHERE c."CertificateNumber" IS NOT NULL
                ) parsed
                WHERE length(parsed."Prefix") <= 20
                  AND length(parsed."Seq") <= 9
                GROUP BY parsed."TenantId", parsed."Prefix", parsed."Year";
                """);

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_certificates_number",
                schema: "toolbox_talks",
                table: "ToolboxTalkCertificates",
                columns: new[] { "TenantId", "CertificateNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_certificate_number_counters_tenant_prefix_year",
                schema: "toolbox_talks",
                table: "CertificateNumberCounters",
                columns: new[] { "TenantId", "Prefix", "Year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CertificateNumberCounters",
                schema: "toolbox_talks");

            migrationBuilder.DropIndex(
                name: "ix_toolbox_talk_certificates_number",
                schema: "toolbox_talks",
                table: "ToolboxTalkCertificates");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_certificates_number",
                schema: "toolbox_talks",
                table: "ToolboxTalkCertificates",
                column: "CertificateNumber",
                unique: true);
        }
    }
}
