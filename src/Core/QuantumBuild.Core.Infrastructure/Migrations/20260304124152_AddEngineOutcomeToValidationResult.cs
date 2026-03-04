using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEngineOutcomeToValidationResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EngineOutcome",
                schema: "toolbox_talks",
                table: "TranslationValidationResults",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EngineOutcome",
                schema: "toolbox_talks",
                table: "TranslationValidationResults");
        }
    }
}
