using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFaithfulExtractionFieldsToRegulatoryRequirement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Block",
                schema: "toolbox_talks",
                table: "RegulatoryRequirements",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeatureIdentifier",
                schema: "toolbox_talks",
                table: "RegulatoryRequirements",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FootnoteDefinition",
                schema: "toolbox_talks",
                table: "RegulatoryRequirements",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerbatimText",
                schema: "toolbox_talks",
                table: "RegulatoryRequirements",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Block",
                schema: "toolbox_talks",
                table: "RegulatoryRequirements");

            migrationBuilder.DropColumn(
                name: "FeatureIdentifier",
                schema: "toolbox_talks",
                table: "RegulatoryRequirements");

            migrationBuilder.DropColumn(
                name: "FootnoteDefinition",
                schema: "toolbox_talks",
                table: "RegulatoryRequirements");

            migrationBuilder.DropColumn(
                name: "VerbatimText",
                schema: "toolbox_talks",
                table: "RegulatoryRequirements");
        }
    }
}
