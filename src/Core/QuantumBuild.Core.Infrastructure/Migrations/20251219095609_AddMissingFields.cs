using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VarianceReason",
                table: "stocktake_lines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostPrice",
                table: "products",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductType",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SellPrice",
                table: "products",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryNoteRef",
                table: "goods_receipts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatchNumber",
                table: "goods_receipt_lines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryDate",
                table: "goods_receipt_lines",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuantityRejected",
                table: "goods_receipt_lines",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "goods_receipt_lines",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VarianceReason",
                table: "stocktake_lines");

            migrationBuilder.DropColumn(
                name: "CostPrice",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ProductType",
                table: "products");

            migrationBuilder.DropColumn(
                name: "SellPrice",
                table: "products");

            migrationBuilder.DropColumn(
                name: "DeliveryNoteRef",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "BatchNumber",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "QuantityRejected",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "goods_receipt_lines");
        }
    }
}
