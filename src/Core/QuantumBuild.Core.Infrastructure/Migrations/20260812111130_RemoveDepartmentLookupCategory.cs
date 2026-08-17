using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDepartmentLookupCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Retires the old Department LookupCategory (superseded by the structured
            // Department entity - see docs/old-department-lookup-retirement-recon.md).
            // IX_LookupCategories_Name is a global unique index (not filtered by
            // IsDeleted), so at most one row can ever be named "Department" - the WHERE
            // clause below is therefore already scoped to exactly that row, never to
            // TrainingCategory/JobTitle/Language. FK_LookupValues_LookupCategories_CategoryId
            // and FK_TenantLookupValues_LookupCategories_CategoryId are both
            // ON DELETE CASCADE, so deleting this row also removes its own LookupValue
            // and TenantLookupValue children (Department has zero LookupValue rows -
            // never seeded - and only tenant-authored TenantLookupValue rows, per the
            // recon). Cascade is FK-scoped to this CategoryId, so no other category's
            // child rows are touched.
            migrationBuilder.Sql(
                """
                DELETE FROM "LookupCategories"
                WHERE "Name" = 'Department';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreates the Department LookupCategory shell so the schema matches
            // pre-migration state. The tenant-authored TenantLookupValue rows deleted by
            // Up() are NOT restored - they were already inert (recon confirmed nothing
            // reads them; no form/report/service consumes the Department lookup
            // category), so this is intentionally a partial, data-lossy rollback of
            // seeded/inert data, not a full reversal.
            migrationBuilder.Sql(
                """
                INSERT INTO "LookupCategories"
                    ("Id", "Name", "Module", "AllowCustom", "IsActive", "CreatedAt", "CreatedBy", "IsDeleted")
                SELECT
                    gen_random_uuid(), 'Department', 'Core', true, true, now(), 'system-migration', false
                WHERE NOT EXISTS (
                    SELECT 1 FROM "LookupCategories" WHERE "Name" = 'Department'
                );
                """);
        }
    }
}
