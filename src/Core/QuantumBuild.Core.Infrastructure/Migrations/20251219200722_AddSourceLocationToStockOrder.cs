using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceLocationToStockOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the column as nullable first
            migrationBuilder.AddColumn<Guid>(
                name: "SourceLocationId",
                table: "stock_orders",
                type: "uuid",
                nullable: true);

            // Set existing orders to use the first warehouse location (WH001)
            migrationBuilder.Sql(@"
                UPDATE stock_orders
                SET ""SourceLocationId"" = (
                    SELECT ""Id""
                    FROM stock_locations
                    WHERE ""LocationCode"" = 'WH001'
                    LIMIT 1
                )
                WHERE ""SourceLocationId"" IS NULL;
            ");

            // Make the column required
            migrationBuilder.AlterColumn<Guid>(
                name: "SourceLocationId",
                table: "stock_orders",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_orders_SourceLocationId",
                table: "stock_orders",
                column: "SourceLocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_stock_orders_stock_locations_SourceLocationId",
                table: "stock_orders",
                column: "SourceLocationId",
                principalTable: "stock_locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stock_orders_stock_locations_SourceLocationId",
                table: "stock_orders");

            migrationBuilder.DropIndex(
                name: "IX_stock_orders_SourceLocationId",
                table: "stock_orders");

            migrationBuilder.DropColumn(
                name: "SourceLocationId",
                table: "stock_orders");
        }
    }
}
