using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStructureMapTerminology : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PersonBlockDescription",
                schema: "toolbox_talks",
                table: "RegulatoryStructureMaps",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "numbered items describing what the person receiving the service experiences");

            migrationBuilder.AddColumn<string>(
                name: "PrincipleLabel",
                schema: "toolbox_talks",
                table: "RegulatoryStructureMaps",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Principle");

            migrationBuilder.AddColumn<string>(
                name: "ProviderBlockDescription",
                schema: "toolbox_talks",
                table: "RegulatoryStructureMaps",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "numbered items describing what the service provider must have in place");

            migrationBuilder.AddColumn<string>(
                name: "StandardLabel",
                schema: "toolbox_talks",
                table: "RegulatoryStructureMaps",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Standard");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PersonBlockDescription",
                schema: "toolbox_talks",
                table: "RegulatoryStructureMaps");

            migrationBuilder.DropColumn(
                name: "PrincipleLabel",
                schema: "toolbox_talks",
                table: "RegulatoryStructureMaps");

            migrationBuilder.DropColumn(
                name: "ProviderBlockDescription",
                schema: "toolbox_talks",
                table: "RegulatoryStructureMaps");

            migrationBuilder.DropColumn(
                name: "StandardLabel",
                schema: "toolbox_talks",
                table: "RegulatoryStructureMaps");
        }
    }
}
