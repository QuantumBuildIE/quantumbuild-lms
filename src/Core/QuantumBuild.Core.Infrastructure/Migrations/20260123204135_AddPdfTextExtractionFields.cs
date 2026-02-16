using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPdfTextExtractionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtractedPdfText",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PdfTextExtractedAt",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtractedPdfText",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "PdfTextExtractedAt",
                schema: "toolbox_talks",
                table: "ToolboxTalks");
        }
    }
}
