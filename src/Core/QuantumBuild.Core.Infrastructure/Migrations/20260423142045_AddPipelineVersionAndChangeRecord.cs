using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineVersionAndChangeRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PipelineVersionId",
                schema: "toolbox_talks",
                table: "TranslationValidationRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PipelineVersions",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Hash = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ComponentsJson = table.Column<string>(type: "text", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PipelineChangeRecords",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Component = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChangeFrom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChangeTo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Justification = table.Column<string>(type: "text", nullable: false),
                    ImpactAssessment = table.Column<string>(type: "text", nullable: true),
                    PriorModulesAction = table.Column<string>(type: "text", nullable: true),
                    Approver = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DeployedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PipelineVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousPipelineVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineChangeRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipelineChangeRecords_PipelineVersions_PipelineVersionId",
                        column: x => x.PipelineVersionId,
                        principalSchema: "toolbox_talks",
                        principalTable: "PipelineVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_translation_validation_runs_pipeline_version",
                schema: "toolbox_talks",
                table: "TranslationValidationRuns",
                column: "PipelineVersionId");

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_change_records_change_id",
                schema: "toolbox_talks",
                table: "PipelineChangeRecords",
                column: "ChangeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_change_records_version",
                schema: "toolbox_talks",
                table: "PipelineChangeRecords",
                column: "PipelineVersionId");

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_versions_hash",
                schema: "toolbox_talks",
                table: "PipelineVersions",
                column: "Hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_versions_is_active",
                schema: "toolbox_talks",
                table: "PipelineVersions",
                column: "IsActive");

            migrationBuilder.AddForeignKey(
                name: "FK_TranslationValidationRuns_PipelineVersions_PipelineVersionId",
                schema: "toolbox_talks",
                table: "TranslationValidationRuns",
                column: "PipelineVersionId",
                principalSchema: "toolbox_talks",
                principalTable: "PipelineVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TranslationValidationRuns_PipelineVersions_PipelineVersionId",
                schema: "toolbox_talks",
                table: "TranslationValidationRuns");

            migrationBuilder.DropTable(
                name: "PipelineChangeRecords",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "PipelineVersions",
                schema: "toolbox_talks");

            migrationBuilder.DropIndex(
                name: "ix_translation_validation_runs_pipeline_version",
                schema: "toolbox_talks",
                table: "TranslationValidationRuns");

            migrationBuilder.DropColumn(
                name: "PipelineVersionId",
                schema: "toolbox_talks",
                table: "TranslationValidationRuns");
        }
    }
}
