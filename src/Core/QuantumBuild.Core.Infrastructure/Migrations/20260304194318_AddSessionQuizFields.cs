using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionQuizFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QuestionsJson",
                schema: "toolbox_talks",
                table: "ContentCreationSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuizSettingsJson",
                schema: "toolbox_talks",
                table: "ContentCreationSessions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuestionsJson",
                schema: "toolbox_talks",
                table: "ContentCreationSessions");

            migrationBuilder.DropColumn(
                name: "QuizSettingsJson",
                schema: "toolbox_talks",
                table: "ContentCreationSessions");
        }
    }
}
