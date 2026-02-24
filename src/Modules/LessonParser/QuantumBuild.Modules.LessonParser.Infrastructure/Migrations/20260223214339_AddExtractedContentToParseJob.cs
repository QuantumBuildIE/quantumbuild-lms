using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Modules.LessonParser.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExtractedContentToParseJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtractedContent",
                schema: "lesson_parser",
                table: "ParseJobs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtractedContent",
                schema: "lesson_parser",
                table: "ParseJobs");
        }
    }
}
