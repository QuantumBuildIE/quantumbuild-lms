using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Modules.LessonParser.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslationStatusToParseJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TranslationFailures",
                schema: "lesson_parser",
                table: "ParseJobs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranslationLanguages",
                schema: "lesson_parser",
                table: "ParseJobs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranslationStatus",
                schema: "lesson_parser",
                table: "ParseJobs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "NotRequired");

            migrationBuilder.AddColumn<int>(
                name: "TranslationsQueued",
                schema: "lesson_parser",
                table: "ParseJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TranslationFailures",
                schema: "lesson_parser",
                table: "ParseJobs");

            migrationBuilder.DropColumn(
                name: "TranslationLanguages",
                schema: "lesson_parser",
                table: "ParseJobs");

            migrationBuilder.DropColumn(
                name: "TranslationStatus",
                schema: "lesson_parser",
                table: "ParseJobs");

            migrationBuilder.DropColumn(
                name: "TranslationsQueued",
                schema: "lesson_parser",
                table: "ParseJobs");
        }
    }
}
