using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubtitleProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubtitleProcessingJobs",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolboxTalkId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SourceVideoUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    VideoSourceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalSubtitles = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    EnglishSrtContent = table.Column<string>(type: "text", nullable: true),
                    EnglishSrtUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubtitleProcessingJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubtitleProcessingJobs_ToolboxTalks_ToolboxTalkId",
                        column: x => x.ToolboxTalkId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubtitleTranslations",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubtitleProcessingJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    SubtitlesProcessed = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TotalSubtitles = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SrtContent = table.Column<string>(type: "text", nullable: true),
                    SrtUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubtitleTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubtitleTranslations_SubtitleProcessingJobs_SubtitleProcess~",
                        column: x => x.SubtitleProcessingJobId,
                        principalSchema: "toolbox_talks",
                        principalTable: "SubtitleProcessingJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_subtitle_processing_jobs_status",
                schema: "toolbox_talks",
                table: "SubtitleProcessingJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_subtitle_processing_jobs_tenant",
                schema: "toolbox_talks",
                table: "SubtitleProcessingJobs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_subtitle_processing_jobs_tenant_status",
                schema: "toolbox_talks",
                table: "SubtitleProcessingJobs",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_subtitle_processing_jobs_tenant_talk_deleted",
                schema: "toolbox_talks",
                table: "SubtitleProcessingJobs",
                columns: new[] { "TenantId", "ToolboxTalkId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "ix_subtitle_processing_jobs_toolbox_talk",
                schema: "toolbox_talks",
                table: "SubtitleProcessingJobs",
                column: "ToolboxTalkId");

            migrationBuilder.CreateIndex(
                name: "ix_subtitle_translations_job",
                schema: "toolbox_talks",
                table: "SubtitleTranslations",
                column: "SubtitleProcessingJobId");

            migrationBuilder.CreateIndex(
                name: "ix_subtitle_translations_job_language",
                schema: "toolbox_talks",
                table: "SubtitleTranslations",
                columns: new[] { "SubtitleProcessingJobId", "LanguageCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subtitle_translations_status",
                schema: "toolbox_talks",
                table: "SubtitleTranslations",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubtitleTranslations",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "SubtitleProcessingJobs",
                schema: "toolbox_talks");
        }
    }
}
