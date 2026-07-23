using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRegulatoryBodyKindAndTenantStandardSubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "toolbox_talks",
                table: "RegulatoryBodies",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Regulation");

            migrationBuilder.AddColumn<Guid>(
                name: "SectorId",
                schema: "toolbox_talks",
                table: "RegulatoryBodies",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TenantStandardSubscriptions",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegulatoryBodyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantStandardSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantStandardSubscriptions_RegulatoryBodies_RegulatoryBody~",
                        column: x => x.RegulatoryBodyId,
                        principalSchema: "toolbox_talks",
                        principalTable: "RegulatoryBodies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantStandardSubscriptions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_regulatory_bodies_sector",
                schema: "toolbox_talks",
                table: "RegulatoryBodies",
                column: "SectorId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_regulatory_bodies_kind_sector",
                schema: "toolbox_talks",
                table: "RegulatoryBodies",
                sql: "(\"Kind\" = 'Standard' AND \"SectorId\" IS NOT NULL) OR (\"Kind\" = 'Regulation' AND \"SectorId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_standard_subscriptions_tenant_body",
                schema: "toolbox_talks",
                table: "TenantStandardSubscriptions",
                columns: new[] { "TenantId", "RegulatoryBodyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantStandardSubscriptions_RegulatoryBodyId",
                schema: "toolbox_talks",
                table: "TenantStandardSubscriptions",
                column: "RegulatoryBodyId");

            migrationBuilder.AddForeignKey(
                name: "FK_RegulatoryBodies_Sectors_SectorId",
                schema: "toolbox_talks",
                table: "RegulatoryBodies",
                column: "SectorId",
                principalSchema: "toolbox_talks",
                principalTable: "Sectors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RegulatoryBodies_Sectors_SectorId",
                schema: "toolbox_talks",
                table: "RegulatoryBodies");

            migrationBuilder.DropTable(
                name: "TenantStandardSubscriptions",
                schema: "toolbox_talks");

            migrationBuilder.DropIndex(
                name: "ix_regulatory_bodies_sector",
                schema: "toolbox_talks",
                table: "RegulatoryBodies");

            migrationBuilder.DropCheckConstraint(
                name: "ck_regulatory_bodies_kind_sector",
                schema: "toolbox_talks",
                table: "RegulatoryBodies");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "toolbox_talks",
                table: "RegulatoryBodies");

            migrationBuilder.DropColumn(
                name: "SectorId",
                schema: "toolbox_talks",
                table: "RegulatoryBodies");
        }
    }
}
