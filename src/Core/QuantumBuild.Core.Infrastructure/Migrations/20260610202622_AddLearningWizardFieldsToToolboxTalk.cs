using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningWizardFieldsToToolboxTalk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AudienceRole",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuditPurpose",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientName",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentRef",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PreserveSourceWording",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReviewerName",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewerOrg",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewerRole",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFileName",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFileType",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFileUrl",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceText",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetLanguageCodes",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudienceRole",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "AuditPurpose",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "ClientName",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "DocumentRef",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "PreserveSourceWording",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "ReviewerName",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "ReviewerOrg",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "ReviewerRole",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "SourceFileName",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "SourceFileType",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "SourceFileUrl",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "SourceText",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "TargetLanguageCodes",
                schema: "toolbox_talks",
                table: "ToolboxTalks");
        }
    }
}
