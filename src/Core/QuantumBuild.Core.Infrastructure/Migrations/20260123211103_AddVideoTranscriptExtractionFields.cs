using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoTranscriptExtractionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtractedVideoTranscript",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VideoTranscriptExtractedAt",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtractedVideoTranscript",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "VideoTranscriptExtractedAt",
                schema: "toolbox_talks",
                table: "ToolboxTalks");
        }
    }
}
