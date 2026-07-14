using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTranscriptWordsJsonToToolboxTalk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InputMode",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Text");

            migrationBuilder.AddColumn<string>(
                name: "TranscriptWordsJson",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InputMode",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "TranscriptWordsJson",
                schema: "toolbox_talks",
                table: "ToolboxTalks");
        }
    }
}
