using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalReviewProvenanceToTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastExternalReviewedAt",
                schema: "toolbox_talks",
                table: "ToolboxTalkTranslations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastExternalReviewedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkTranslations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastExternalReviewedAt",
                schema: "toolbox_talks",
                table: "ToolboxTalkTranslations");

            migrationBuilder.DropColumn(
                name: "LastExternalReviewedBy",
                schema: "toolbox_talks",
                table: "ToolboxTalkTranslations");
        }
    }
}
