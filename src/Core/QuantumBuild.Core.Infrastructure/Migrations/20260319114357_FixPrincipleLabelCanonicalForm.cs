using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixPrincipleLabelCanonicalForm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix PrincipleLabel inconsistency: "Safety and Wellbeing" → "Safety & Wellbeing"
            // AI-ingested requirements may have used "and" instead of the canonical "&"
            migrationBuilder.Sql(
                """
                UPDATE toolbox_talks."RegulatoryRequirements"
                SET "PrincipleLabel" = 'Safety & Wellbeing',
                    "UpdatedAt" = NOW(),
                    "UpdatedBy" = 'migration'
                WHERE "PrincipleLabel" = 'Safety and Wellbeing'
                  AND "IsDeleted" = false;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No rollback — the canonical form is the correct one
        }
    }
}
