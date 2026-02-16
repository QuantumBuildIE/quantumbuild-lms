using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFileHashDeduplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ContentGeneratedAt",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfFileHash",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoFileHash",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talks_tenant_pdf_hash",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                columns: new[] { "TenantId", "PdfFileHash" },
                filter: "\"PdfFileHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talks_tenant_video_hash",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                columns: new[] { "TenantId", "VideoFileHash" },
                filter: "\"VideoFileHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_toolbox_talks_tenant_pdf_hash",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropIndex(
                name: "ix_toolbox_talks_tenant_video_hash",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "ContentGeneratedAt",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "PdfFileHash",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "VideoFileHash",
                schema: "toolbox_talks",
                table: "ToolboxTalks");
        }
    }
}
