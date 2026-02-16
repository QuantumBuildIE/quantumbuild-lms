using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBayLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "VarianceReason",
                table: "stocktake_lines",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BayCode",
                table: "stocktake_lines",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BayLocationId",
                table: "stocktake_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BayLocationId",
                table: "stock_levels",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BayLocationId",
                table: "goods_receipt_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bay_locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BayCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StockLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bay_locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bay_locations_stock_locations_StockLocationId",
                        column: x => x.StockLocationId,
                        principalTable: "stock_locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stock_levels_BayLocationId",
                table: "stock_levels",
                column: "BayLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_BayLocationId",
                table: "goods_receipt_lines",
                column: "BayLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_bay_locations_IsActive",
                table: "bay_locations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_bay_locations_StockLocationId",
                table: "bay_locations",
                column: "StockLocationId");

            migrationBuilder.CreateIndex(
                name: "ix_bay_locations_tenant_location_code",
                table: "bay_locations",
                columns: new[] { "TenantId", "StockLocationId", "BayCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bay_locations_TenantId",
                table: "bay_locations",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_goods_receipt_lines_bay_locations_BayLocationId",
                table: "goods_receipt_lines",
                column: "BayLocationId",
                principalTable: "bay_locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_levels_bay_locations_BayLocationId",
                table: "stock_levels",
                column: "BayLocationId",
                principalTable: "bay_locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_goods_receipt_lines_bay_locations_BayLocationId",
                table: "goods_receipt_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_levels_bay_locations_BayLocationId",
                table: "stock_levels");

            migrationBuilder.DropTable(
                name: "bay_locations");

            migrationBuilder.DropIndex(
                name: "IX_stock_levels_BayLocationId",
                table: "stock_levels");

            migrationBuilder.DropIndex(
                name: "IX_goods_receipt_lines_BayLocationId",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "BayCode",
                table: "stocktake_lines");

            migrationBuilder.DropColumn(
                name: "BayLocationId",
                table: "stocktake_lines");

            migrationBuilder.DropColumn(
                name: "BayLocationId",
                table: "stock_levels");

            migrationBuilder.DropColumn(
                name: "BayLocationId",
                table: "goods_receipt_lines");

            migrationBuilder.AlterColumn<string>(
                name: "VarianceReason",
                table: "stocktake_lines",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
