using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslationQualityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreFlightScanJson",
                schema: "toolbox_talks",
                table: "TranslationValidationRuns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArtefactsJson",
                schema: "toolbox_talks",
                table: "TranslationValidationResults",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistryViolationsJson",
                schema: "toolbox_talks",
                table: "TranslationValidationResults",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewReasonsJson",
                schema: "toolbox_talks",
                table: "TranslationValidationResults",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreFlightScanJson",
                schema: "toolbox_talks",
                table: "TranslationValidationRuns");

            migrationBuilder.DropColumn(
                name: "ArtefactsJson",
                schema: "toolbox_talks",
                table: "TranslationValidationResults");

            migrationBuilder.DropColumn(
                name: "RegistryViolationsJson",
                schema: "toolbox_talks",
                table: "TranslationValidationResults");

            migrationBuilder.DropColumn(
                name: "ReviewReasonsJson",
                schema: "toolbox_talks",
                table: "TranslationValidationResults");
        }
    }
}
