using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTranscriptWordsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TranscriptWordsJson",
                schema: "toolbox_talks",
                table: "ContentCreationSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranscriptionJobId",
                schema: "toolbox_talks",
                table: "ContentCreationSessions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TranscriptWordsJson",
                schema: "toolbox_talks",
                table: "ContentCreationSessions");

            migrationBuilder.DropColumn(
                name: "TranscriptionJobId",
                schema: "toolbox_talks",
                table: "ContentCreationSessions");
        }
    }
}
