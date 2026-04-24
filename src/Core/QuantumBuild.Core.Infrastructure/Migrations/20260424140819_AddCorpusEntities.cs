using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCorpusEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "toolbox_talks",
                table: "PipelineChangeRecords",
                type: "text",
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.CreateTable(
                name: "AuditCorpora",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CorpusId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SectorKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LanguagePair = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceTalkId = table.Column<Guid>(type: "uuid", nullable: true),
                    FrozenFromPipelineVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LockedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SignedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
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
                    table.PrimaryKey("PK_AuditCorpora", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditCorpora_PipelineVersions_FrozenFromPipelineVersionId",
                        column: x => x.FrozenFromPipelineVersionId,
                        principalSchema: "toolbox_talks",
                        principalTable: "PipelineVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AuditCorpora_ToolboxTalks_SourceTalkId",
                        column: x => x.SourceTalkId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AuditCorpusEntries",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CorpusId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryRef = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SectionTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OriginalText = table.Column<string>(type: "text", nullable: false),
                    TranslatedText = table.Column<string>(type: "text", nullable: false),
                    SourceLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TargetLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SectorKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PassThreshold = table.Column<int>(type: "integer", nullable: false),
                    ExpectedOutcome = table.Column<string>(type: "text", nullable: false),
                    IsSafetyCritical = table.Column<bool>(type: "boolean", nullable: false),
                    PipelineVersionIdAtFreeze = table.Column<Guid>(type: "uuid", nullable: true),
                    TagsJson = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditCorpusEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditCorpusEntries_AuditCorpora_CorpusId",
                        column: x => x.CorpusId,
                        principalSchema: "toolbox_talks",
                        principalTable: "AuditCorpora",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CorpusRuns",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CorpusId = table.Column<Guid>(type: "uuid", nullable: false),
                    PipelineVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedPipelineChangeId = table.Column<Guid>(type: "uuid", nullable: true),
                    TriggerType = table.Column<string>(type: "text", nullable: false),
                    TriggeredBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsSmokeTest = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TotalEntries = table.Column<int>(type: "integer", nullable: false),
                    PassedEntries = table.Column<int>(type: "integer", nullable: false),
                    ReviewEntries = table.Column<int>(type: "integer", nullable: false),
                    FailedEntries = table.Column<int>(type: "integer", nullable: false),
                    RegressionEntries = table.Column<int>(type: "integer", nullable: false),
                    MeanScore = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    MaxScoreDrop = table.Column<int>(type: "integer", nullable: true),
                    Verdict = table.Column<string>(type: "text", nullable: true),
                    FailureThresholdPercent = table.Column<int>(type: "integer", nullable: false, defaultValue: 20),
                    ScoreDropThreshold = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    EstimatedCostEur = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    ActualCostEur = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_CorpusRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorpusRuns_AuditCorpora_CorpusId",
                        column: x => x.CorpusId,
                        principalSchema: "toolbox_talks",
                        principalTable: "AuditCorpora",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CorpusRuns_PipelineChangeRecords_LinkedPipelineChangeId",
                        column: x => x.LinkedPipelineChangeId,
                        principalSchema: "toolbox_talks",
                        principalTable: "PipelineChangeRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CorpusRuns_PipelineVersions_PipelineVersionId",
                        column: x => x.PipelineVersionId,
                        principalSchema: "toolbox_talks",
                        principalTable: "PipelineVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProviderResultCache",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CorpusEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BackTranslation = table.Column<string>(type: "text", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderResultCache", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderResultCache_AuditCorpusEntries_CorpusEntryId",
                        column: x => x.CorpusEntryId,
                        principalSchema: "toolbox_talks",
                        principalTable: "AuditCorpusEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CorpusRunResults",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CorpusRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorpusEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinalScore = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    ExpectedOutcome = table.Column<string>(type: "text", nullable: false),
                    IsRegression = table.Column<bool>(type: "boolean", nullable: false),
                    ScoreDelta = table.Column<int>(type: "integer", nullable: true),
                    RoundsUsed = table.Column<int>(type: "integer", nullable: false),
                    IsSafetyCritical = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveThreshold = table.Column<int>(type: "integer", nullable: false),
                    BackTranslationA = table.Column<string>(type: "text", nullable: true),
                    BackTranslationB = table.Column<string>(type: "text", nullable: true),
                    BackTranslationC = table.Column<string>(type: "text", nullable: true),
                    BackTranslationD = table.Column<string>(type: "text", nullable: true),
                    ScoreA = table.Column<int>(type: "integer", nullable: true),
                    ScoreB = table.Column<int>(type: "integer", nullable: true),
                    ScoreC = table.Column<int>(type: "integer", nullable: true),
                    ScoreD = table.Column<int>(type: "integer", nullable: true),
                    GlossaryCorrectionsJson = table.Column<string>(type: "text", nullable: true),
                    ArtefactsJson = table.Column<string>(type: "text", nullable: true),
                    ReviewReasonsJson = table.Column<string>(type: "text", nullable: true),
                    WasCached = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorpusRunResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorpusRunResults_AuditCorpusEntries_CorpusEntryId",
                        column: x => x.CorpusEntryId,
                        principalSchema: "toolbox_talks",
                        principalTable: "AuditCorpusEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorpusRunResults_CorpusRuns_CorpusRunId",
                        column: x => x.CorpusRunId,
                        principalSchema: "toolbox_talks",
                        principalTable: "CorpusRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_corpora_tenant_id_corpus_id",
                schema: "toolbox_talks",
                table: "AuditCorpora",
                columns: new[] { "TenantId", "CorpusId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditCorpora_FrozenFromPipelineVersionId",
                schema: "toolbox_talks",
                table: "AuditCorpora",
                column: "FrozenFromPipelineVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditCorpora_SourceTalkId",
                schema: "toolbox_talks",
                table: "AuditCorpora",
                column: "SourceTalkId");

            migrationBuilder.CreateIndex(
                name: "ix_audit_corpus_entries_corpus_id",
                schema: "toolbox_talks",
                table: "AuditCorpusEntries",
                column: "CorpusId");

            migrationBuilder.CreateIndex(
                name: "ix_corpus_run_results_corpus_run_id",
                schema: "toolbox_talks",
                table: "CorpusRunResults",
                column: "CorpusRunId");

            migrationBuilder.CreateIndex(
                name: "IX_CorpusRunResults_CorpusEntryId",
                schema: "toolbox_talks",
                table: "CorpusRunResults",
                column: "CorpusEntryId");

            migrationBuilder.CreateIndex(
                name: "ix_corpus_runs_corpus_id_status",
                schema: "toolbox_talks",
                table: "CorpusRuns",
                columns: new[] { "CorpusId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_corpus_runs_tenant_id",
                schema: "toolbox_talks",
                table: "CorpusRuns",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CorpusRuns_LinkedPipelineChangeId",
                schema: "toolbox_talks",
                table: "CorpusRuns",
                column: "LinkedPipelineChangeId");

            migrationBuilder.CreateIndex(
                name: "IX_CorpusRuns_PipelineVersionId",
                schema: "toolbox_talks",
                table: "CorpusRuns",
                column: "PipelineVersionId");

            migrationBuilder.CreateIndex(
                name: "ix_provider_result_cache_entry_provider_version",
                schema: "toolbox_talks",
                table: "ProviderResultCache",
                columns: new[] { "CorpusEntryId", "Provider", "ProviderVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorpusRunResults",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "ProviderResultCache",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "CorpusRuns",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "AuditCorpusEntries",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "AuditCorpora",
                schema: "toolbox_talks");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "toolbox_talks",
                table: "PipelineChangeRecords");
        }
    }
}
