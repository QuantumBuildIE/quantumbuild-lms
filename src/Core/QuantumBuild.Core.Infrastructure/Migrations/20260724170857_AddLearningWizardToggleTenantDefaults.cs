using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningWizardToggleTenantDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DefaultAllowRetry",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultAutoAssign",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultGenerateSlideshow",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultIncludeQuiz",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultPreserveSourceWording",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultShuffleOptions",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultShuffleQuestions",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultUseQuestionPool",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultVideoRightsConfirmed",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultAllowRetry",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings");

            migrationBuilder.DropColumn(
                name: "DefaultAutoAssign",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings");

            migrationBuilder.DropColumn(
                name: "DefaultGenerateSlideshow",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings");

            migrationBuilder.DropColumn(
                name: "DefaultIncludeQuiz",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings");

            migrationBuilder.DropColumn(
                name: "DefaultPreserveSourceWording",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings");

            migrationBuilder.DropColumn(
                name: "DefaultShuffleOptions",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings");

            migrationBuilder.DropColumn(
                name: "DefaultShuffleQuestions",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings");

            migrationBuilder.DropColumn(
                name: "DefaultUseQuestionPool",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings");

            migrationBuilder.DropColumn(
                name: "DefaultVideoRightsConfirmed",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings");
        }
    }
}
