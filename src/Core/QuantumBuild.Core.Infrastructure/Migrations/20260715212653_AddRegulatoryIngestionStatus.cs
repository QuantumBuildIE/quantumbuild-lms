using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRegulatoryIngestionStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastIngestionErrorCode",
                schema: "toolbox_talks",
                table: "RegulatoryDocuments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastIngestionErrorMessage",
                schema: "toolbox_talks",
                table: "RegulatoryDocuments",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastIngestionStatus",
                schema: "toolbox_talks",
                table: "RegulatoryDocuments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Idle");

            // Backfill existing rows: a document that was successfully ingested before this
            // column existed (LastIngestedAt already populated) should read as "Success", not
            // the blanket "Idle" default — only documents that have genuinely never been
            // ingested should show Idle.
            migrationBuilder.Sql(
                """
                UPDATE toolbox_talks."RegulatoryDocuments"
                SET "LastIngestionStatus" = 'Success'
                WHERE "LastIngestedAt" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastIngestionErrorCode",
                schema: "toolbox_talks",
                table: "RegulatoryDocuments");

            migrationBuilder.DropColumn(
                name: "LastIngestionErrorMessage",
                schema: "toolbox_talks",
                table: "RegulatoryDocuments");

            migrationBuilder.DropColumn(
                name: "LastIngestionStatus",
                schema: "toolbox_talks",
                table: "RegulatoryDocuments");
        }
    }
}
