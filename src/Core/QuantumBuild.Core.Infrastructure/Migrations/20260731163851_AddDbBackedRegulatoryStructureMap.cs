using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDbBackedRegulatoryStructureMap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegulatoryStructureMaps",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegulatoryDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    VerifiedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegulatoryStructureMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegulatoryStructureMaps_RegulatoryDocuments_RegulatoryDocum~",
                        column: x => x.RegulatoryDocumentId,
                        principalSchema: "toolbox_talks",
                        principalTable: "RegulatoryDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegulatoryStructureMapPrinciples",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegulatoryStructureMapId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegulatoryStructureMapPrinciples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegulatoryStructureMapPrinciples_RegulatoryStructureMaps_Re~",
                        column: x => x.RegulatoryStructureMapId,
                        principalSchema: "toolbox_talks",
                        principalTable: "RegulatoryStructureMaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegulatoryStructureMapStandards",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegulatoryStructureMapPrincipleId = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegulatoryStructureMapStandards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegulatoryStructureMapStandards_RegulatoryStructureMapPrinc~",
                        column: x => x.RegulatoryStructureMapPrincipleId,
                        principalSchema: "toolbox_talks",
                        principalTable: "RegulatoryStructureMapPrinciples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegulatoryStructureMapFeatures",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegulatoryStructureMapStandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Identifier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Block = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VerbatimText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    FootnoteDefinition = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegulatoryStructureMapFeatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegulatoryStructureMapFeatures_RegulatoryStructureMapStanda~",
                        column: x => x.RegulatoryStructureMapStandardId,
                        principalSchema: "toolbox_talks",
                        principalTable: "RegulatoryStructureMapStandards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_regulatory_structure_map_features_standard_block_identifier",
                schema: "toolbox_talks",
                table: "RegulatoryStructureMapFeatures",
                columns: new[] { "RegulatoryStructureMapStandardId", "Block", "Identifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_regulatory_structure_map_principles_map_number",
                schema: "toolbox_talks",
                table: "RegulatoryStructureMapPrinciples",
                columns: new[] { "RegulatoryStructureMapId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_regulatory_structure_maps_document",
                schema: "toolbox_talks",
                table: "RegulatoryStructureMaps",
                column: "RegulatoryDocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_regulatory_structure_map_standards_principle_standard",
                schema: "toolbox_talks",
                table: "RegulatoryStructureMapStandards",
                columns: new[] { "RegulatoryStructureMapPrincipleId", "StandardId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegulatoryStructureMapFeatures",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "RegulatoryStructureMapStandards",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "RegulatoryStructureMapPrinciples",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "RegulatoryStructureMaps",
                schema: "toolbox_talks");
        }
    }
}
