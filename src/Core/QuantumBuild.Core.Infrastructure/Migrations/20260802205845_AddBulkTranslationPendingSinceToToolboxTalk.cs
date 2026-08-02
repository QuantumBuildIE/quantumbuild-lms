using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBulkTranslationPendingSinceToToolboxTalk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "BulkTranslationPendingSince",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talks_tenant_bulk_translation_pending",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                columns: new[] { "TenantId", "BulkTranslationPendingSince" },
                filter: "\"BulkTranslationPendingSince\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_toolbox_talks_tenant_bulk_translation_pending",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "BulkTranslationPendingSince",
                schema: "toolbox_talks",
                table: "ToolboxTalks");
        }
    }
}
