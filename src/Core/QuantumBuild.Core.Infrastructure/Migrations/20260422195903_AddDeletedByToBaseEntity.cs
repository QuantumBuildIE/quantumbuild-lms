using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeletedByToBaseEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ValidationRegulatoryScores",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "TranslationValidationRuns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "TranslationValidationResults",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkVideoTranslations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkTranslations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkSlideTranslations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkSlideshowTranslations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkSlides",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkSections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkSchedules",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkScheduleAssignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkQuestions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseTranslations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseAssignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkCertificates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "TenantSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "TenantSectors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "TenantModules",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "TenantLookupValues",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "SupervisorAssignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "SubtitleTranslations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "SubtitleProcessingJobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Sites",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "Sectors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ScheduledTalkSectionProgress",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ScheduledTalks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ScheduledTalkQuizAttempts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ScheduledTalkCompletions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "SafetyGlossaryTerms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "SafetyGlossaries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "RegulatoryRequirements",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "RegulatoryRequirementMappings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "RegulatoryProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "RegulatoryDocuments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "RegulatoryCriteria",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "RegulatoryBodies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Permissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "LookupValues",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "LookupCategories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Employees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "DpaAcceptances",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ContentCreationSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Contacts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "AiUsageSummaries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "AiUsageLogs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ValidationRegulatoryScores");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "TranslationValidationRuns");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "TranslationValidationResults");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkVideoTranslations");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkTranslations");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkSlideTranslations");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkSlideshowTranslations");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkSlides");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkSections");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkSchedules");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkScheduleAssignments");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkQuestions");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseTranslations");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourses");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseItems");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseAssignments");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkCertificates");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "TenantSectors");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "TenantModules");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "TenantLookupValues");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "SupervisorAssignments");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "SubtitleTranslations");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "SubtitleProcessingJobs");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "Sectors");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ScheduledTalkSectionProgress");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ScheduledTalks");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ScheduledTalkQuizAttempts");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ScheduledTalkCompletions");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "SafetyGlossaryTerms");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "SafetyGlossaries");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "RegulatoryRequirements");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "RegulatoryRequirementMappings");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "RegulatoryProfiles");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "RegulatoryDocuments");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "RegulatoryCriteria");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "RegulatoryBodies");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "LookupValues");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "LookupCategories");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "DpaAcceptances");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "ContentCreationSessions");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "AiUsageSummaries");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "toolbox_talks",
                table: "AiUsageLogs");
        }
    }
}
