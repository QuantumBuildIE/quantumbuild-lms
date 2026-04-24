using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslationDeviation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TranslationDeviations",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviationId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DetectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DetectedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ValidationRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ValidationResultId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModuleRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LessonRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LanguagePair = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SourceExcerpt = table.Column<string>(type: "text", nullable: true),
                    TargetExcerpt = table.Column<string>(type: "text", nullable: true),
                    Nature = table.Column<string>(type: "text", nullable: false),
                    RootCauseCategory = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RootCauseDetail = table.Column<string>(type: "text", nullable: true),
                    CorrectiveAction = table.Column<string>(type: "text", nullable: true),
                    PreventiveAction = table.Column<string>(type: "text", nullable: true),
                    Approver = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ClosedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PipelineVersionAtTime = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("PK_TranslationDeviations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TranslationDeviations_TranslationValidationRuns_ValidationR~",
                        column: x => x.ValidationRunId,
                        principalSchema: "toolbox_talks",
                        principalTable: "TranslationValidationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_translation_deviations_tenant_deviation_id",
                schema: "toolbox_talks",
                table: "TranslationDeviations",
                columns: new[] { "TenantId", "DeviationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_translation_deviations_tenant_run",
                schema: "toolbox_talks",
                table: "TranslationDeviations",
                columns: new[] { "TenantId", "ValidationRunId" });

            migrationBuilder.CreateIndex(
                name: "ix_translation_deviations_tenant_status_detected",
                schema: "toolbox_talks",
                table: "TranslationDeviations",
                columns: new[] { "TenantId", "Status", "DetectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TranslationDeviations_ValidationRunId",
                schema: "toolbox_talks",
                table: "TranslationDeviations",
                column: "ValidationRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TranslationDeviations",
                schema: "toolbox_talks");
        }
    }
}
