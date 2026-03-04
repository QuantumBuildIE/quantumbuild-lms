using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslationValidationAndSafetyGlossary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SafetyGlossaries",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SectorKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SectorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SectorIcon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyGlossaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TranslationValidationRuns",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolboxTalkId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LanguageCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SectorKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PassThreshold = table.Column<int>(type: "integer", nullable: false),
                    SourceLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SourceDialect = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OverallScore = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    OverallOutcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SafetyVerdict = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TotalSections = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PassedSections = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ReviewSections = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    FailedSections = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ReviewerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReviewerOrg = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReviewerRole = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DocumentRef = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AuditPurpose = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditReportUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranslationValidationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TranslationValidationRuns_ToolboxTalkCourses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalkCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TranslationValidationRuns_ToolboxTalks_ToolboxTalkId",
                        column: x => x.ToolboxTalkId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SafetyGlossaryTerms",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GlossaryId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnglishTerm = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsCritical = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Translations = table.Column<string>(type: "text", nullable: false, defaultValue: "{}"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyGlossaryTerms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SafetyGlossaryTerms_SafetyGlossaries_GlossaryId",
                        column: x => x.GlossaryId,
                        principalSchema: "toolbox_talks",
                        principalTable: "SafetyGlossaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TranslationValidationResults",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ValidationRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionIndex = table.Column<int>(type: "integer", nullable: false),
                    SectionTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OriginalText = table.Column<string>(type: "text", nullable: false),
                    TranslatedText = table.Column<string>(type: "text", nullable: false),
                    BackTranslationA = table.Column<string>(type: "text", nullable: true),
                    BackTranslationB = table.Column<string>(type: "text", nullable: true),
                    BackTranslationC = table.Column<string>(type: "text", nullable: true),
                    BackTranslationD = table.Column<string>(type: "text", nullable: true),
                    ScoreA = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ScoreB = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ScoreC = table.Column<int>(type: "integer", nullable: true),
                    ScoreD = table.Column<int>(type: "integer", nullable: true),
                    FinalScore = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RoundsUsed = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsSafetyCritical = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CriticalTerms = table.Column<string>(type: "text", nullable: true),
                    GlossaryMismatches = table.Column<string>(type: "text", nullable: true),
                    EffectiveThreshold = table.Column<int>(type: "integer", nullable: false),
                    ReviewerDecision = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    EditedTranslation = table.Column<string>(type: "text", nullable: true),
                    DecisionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranslationValidationResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TranslationValidationResults_TranslationValidationRuns_Vali~",
                        column: x => x.ValidationRunId,
                        principalSchema: "toolbox_talks",
                        principalTable: "TranslationValidationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_safety_glossaries_tenant",
                schema: "toolbox_talks",
                table: "SafetyGlossaries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_safety_glossaries_tenant_sector",
                schema: "toolbox_talks",
                table: "SafetyGlossaries",
                columns: new[] { "TenantId", "SectorKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_safety_glossary_terms_category",
                schema: "toolbox_talks",
                table: "SafetyGlossaryTerms",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "ix_safety_glossary_terms_glossary",
                schema: "toolbox_talks",
                table: "SafetyGlossaryTerms",
                column: "GlossaryId");

            migrationBuilder.CreateIndex(
                name: "ix_safety_glossary_terms_glossary_term",
                schema: "toolbox_talks",
                table: "SafetyGlossaryTerms",
                columns: new[] { "GlossaryId", "EnglishTerm" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_translation_validation_results_decision",
                schema: "toolbox_talks",
                table: "TranslationValidationResults",
                column: "ReviewerDecision");

            migrationBuilder.CreateIndex(
                name: "ix_translation_validation_results_outcome",
                schema: "toolbox_talks",
                table: "TranslationValidationResults",
                column: "Outcome");

            migrationBuilder.CreateIndex(
                name: "ix_translation_validation_results_run",
                schema: "toolbox_talks",
                table: "TranslationValidationResults",
                column: "ValidationRunId");

            migrationBuilder.CreateIndex(
                name: "ix_translation_validation_results_run_section",
                schema: "toolbox_talks",
                table: "TranslationValidationResults",
                columns: new[] { "ValidationRunId", "SectionIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_translation_validation_runs_course",
                schema: "toolbox_talks",
                table: "TranslationValidationRuns",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "ix_translation_validation_runs_status",
                schema: "toolbox_talks",
                table: "TranslationValidationRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_translation_validation_runs_talk",
                schema: "toolbox_talks",
                table: "TranslationValidationRuns",
                column: "ToolboxTalkId");

            migrationBuilder.CreateIndex(
                name: "ix_translation_validation_runs_tenant",
                schema: "toolbox_talks",
                table: "TranslationValidationRuns",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_translation_validation_runs_tenant_course_lang",
                schema: "toolbox_talks",
                table: "TranslationValidationRuns",
                columns: new[] { "TenantId", "CourseId", "LanguageCode" });

            migrationBuilder.CreateIndex(
                name: "ix_translation_validation_runs_tenant_talk_lang",
                schema: "toolbox_talks",
                table: "TranslationValidationRuns",
                columns: new[] { "TenantId", "ToolboxTalkId", "LanguageCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SafetyGlossaryTerms",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "TranslationValidationResults",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "SafetyGlossaries",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "TranslationValidationRuns",
                schema: "toolbox_talks");
        }
    }
}
