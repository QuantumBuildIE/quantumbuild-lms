using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddToolboxTalkAIGenerationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                schema: "toolbox_talks",
                table: "ToolboxTalkSections",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AddColumn<string>(
                name: "VideoTimestamp",
                schema: "toolbox_talks",
                table: "ToolboxTalkSections",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GeneratedFromPdf",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "GeneratedFromVideo",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PdfFileName",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfUrl",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<bool>(
                name: "IsFromVideoFinalPortion",
                schema: "toolbox_talks",
                table: "ToolboxTalkQuestions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                schema: "toolbox_talks",
                table: "ToolboxTalkQuestions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AddColumn<string>(
                name: "VideoTimestamp",
                schema: "toolbox_talks",
                table: "ToolboxTalkQuestions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Source",
                schema: "toolbox_talks",
                table: "ToolboxTalkSections");

            migrationBuilder.DropColumn(
                name: "VideoTimestamp",
                schema: "toolbox_talks",
                table: "ToolboxTalkSections");

            migrationBuilder.DropColumn(
                name: "GeneratedFromPdf",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "GeneratedFromVideo",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "PdfFileName",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "PdfUrl",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "IsFromVideoFinalPortion",
                schema: "toolbox_talks",
                table: "ToolboxTalkQuestions");

            migrationBuilder.DropColumn(
                name: "Source",
                schema: "toolbox_talks",
                table: "ToolboxTalkQuestions");

            migrationBuilder.DropColumn(
                name: "VideoTimestamp",
                schema: "toolbox_talks",
                table: "ToolboxTalkQuestions");
        }
    }
}
