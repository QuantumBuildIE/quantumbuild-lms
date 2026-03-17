using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRegulatoryProfileChain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegulatoryBodies",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Website = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegulatoryBodies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegulatoryDocuments",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegulatoryBodyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SourceUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegulatoryDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegulatoryDocuments_RegulatoryBodies_RegulatoryBodyId",
                        column: x => x.RegulatoryBodyId,
                        principalSchema: "toolbox_talks",
                        principalTable: "RegulatoryBodies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegulatoryProfiles",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegulatoryDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectorKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ScoreLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExportLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CategoryWeightsJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegulatoryProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegulatoryProfiles_RegulatoryDocuments_RegulatoryDocumentId",
                        column: x => x.RegulatoryDocumentId,
                        principalSchema: "toolbox_talks",
                        principalTable: "RegulatoryDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegulatoryProfiles_Sectors_SectorId",
                        column: x => x.SectorId,
                        principalSchema: "toolbox_talks",
                        principalTable: "Sectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegulatoryCriteria",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegulatoryProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CriteriaText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Source = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegulatoryCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegulatoryCriteria_RegulatoryProfiles_RegulatoryProfileId",
                        column: x => x.RegulatoryProfileId,
                        principalSchema: "toolbox_talks",
                        principalTable: "RegulatoryProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegulatoryCriteria_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_regulatory_bodies_code",
                schema: "toolbox_talks",
                table: "RegulatoryBodies",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_regulatory_criteria_profile_tenant_category_order",
                schema: "toolbox_talks",
                table: "RegulatoryCriteria",
                columns: new[] { "RegulatoryProfileId", "TenantId", "CategoryKey", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_regulatory_criteria_tenant",
                schema: "toolbox_talks",
                table: "RegulatoryCriteria",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_regulatory_documents_body",
                schema: "toolbox_talks",
                table: "RegulatoryDocuments",
                column: "RegulatoryBodyId");

            migrationBuilder.CreateIndex(
                name: "ix_regulatory_profiles_document_sector",
                schema: "toolbox_talks",
                table: "RegulatoryProfiles",
                columns: new[] { "RegulatoryDocumentId", "SectorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_regulatory_profiles_sector_key",
                schema: "toolbox_talks",
                table: "RegulatoryProfiles",
                column: "SectorKey");

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryProfiles_SectorId",
                schema: "toolbox_talks",
                table: "RegulatoryProfiles",
                column: "SectorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegulatoryCriteria",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "RegulatoryProfiles",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "RegulatoryDocuments",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "RegulatoryBodies",
                schema: "toolbox_talks");
        }
    }
}
