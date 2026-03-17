using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddValidationRegulatoryScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ValidationRegulatoryScores",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ValidationRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScoreType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RegulatoryProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    OverallScore = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CategoryScoresJson = table.Column<string>(type: "text", nullable: false),
                    Verdict = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RunLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RunNumber = table.Column<int>(type: "integer", nullable: false),
                    FullResponseJson = table.Column<string>(type: "text", nullable: false),
                    ScoredSectionCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TargetLanguage = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RegulatoryBody = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationRegulatoryScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValidationRegulatoryScores_RegulatoryProfiles_RegulatoryPro~",
                        column: x => x.RegulatoryProfileId,
                        principalSchema: "toolbox_talks",
                        principalTable: "RegulatoryProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ValidationRegulatoryScores_TranslationValidationRuns_Valida~",
                        column: x => x.ValidationRunId,
                        principalSchema: "toolbox_talks",
                        principalTable: "TranslationValidationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_validation_regulatory_scores_regulatory_profile",
                schema: "toolbox_talks",
                table: "ValidationRegulatoryScores",
                column: "RegulatoryProfileId");

            migrationBuilder.CreateIndex(
                name: "ix_validation_regulatory_scores_run",
                schema: "toolbox_talks",
                table: "ValidationRegulatoryScores",
                column: "ValidationRunId");

            migrationBuilder.CreateIndex(
                name: "ix_validation_regulatory_scores_run_type",
                schema: "toolbox_talks",
                table: "ValidationRegulatoryScores",
                columns: new[] { "ValidationRunId", "ScoreType" });

            migrationBuilder.CreateIndex(
                name: "ix_validation_regulatory_scores_tenant",
                schema: "toolbox_talks",
                table: "ValidationRegulatoryScores",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ValidationRegulatoryScores",
                schema: "toolbox_talks");
        }
    }
}
