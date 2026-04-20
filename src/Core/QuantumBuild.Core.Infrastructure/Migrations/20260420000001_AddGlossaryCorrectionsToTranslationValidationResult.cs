using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGlossaryCorrectionsToTranslationValidationResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GlossaryCorrectionsJson",
                schema: "toolbox_talks",
                table: "TranslationValidationResults",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GlossaryHardBlockApplied",
                schema: "toolbox_talks",
                table: "TranslationValidationResults",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GlossaryCorrectionsJson",
                schema: "toolbox_talks",
                table: "TranslationValidationResults");

            migrationBuilder.DropColumn(
                name: "GlossaryHardBlockApplied",
                schema: "toolbox_talks",
                table: "TranslationValidationResults");
        }
    }
}
