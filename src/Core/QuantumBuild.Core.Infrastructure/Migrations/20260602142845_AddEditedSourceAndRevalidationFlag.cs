using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEditedSourceAndRevalidationFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EditedSource",
                schema: "toolbox_talks",
                table: "TranslationValidationResults",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsRevalidation",
                schema: "toolbox_talks",
                table: "ToolboxTalkTranslations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EditedSource",
                schema: "toolbox_talks",
                table: "TranslationValidationResults");

            migrationBuilder.DropColumn(
                name: "NeedsRevalidation",
                schema: "toolbox_talks",
                table: "ToolboxTalkTranslations");
        }
    }
}
