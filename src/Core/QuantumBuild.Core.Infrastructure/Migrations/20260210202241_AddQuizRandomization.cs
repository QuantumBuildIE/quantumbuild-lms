using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizRandomization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "QuizQuestionCount",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShuffleOptions",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShuffleQuestions",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UseQuestionPool",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GeneratedQuestionsJson",
                schema: "toolbox_talks",
                table: "ScheduledTalkQuizAttempts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuizQuestionCount",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "ShuffleOptions",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "ShuffleQuestions",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "UseQuestionPool",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "GeneratedQuestionsJson",
                schema: "toolbox_talks",
                table: "ScheduledTalkQuizAttempts");
        }
    }
}
