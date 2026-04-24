using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeePinFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QrPin",
                table: "Employees",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "QrPinIsSet",
                table: "Employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "QrPinGeneratedAt",
                table: "Employees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "QrPinLastUsedAt",
                table: "Employees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QrPinFailedAttempts",
                table: "Employees",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "QrPinLockedUntil",
                table: "Employees",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "QrPin", table: "Employees");
            migrationBuilder.DropColumn(name: "QrPinIsSet", table: "Employees");
            migrationBuilder.DropColumn(name: "QrPinGeneratedAt", table: "Employees");
            migrationBuilder.DropColumn(name: "QrPinLastUsedAt", table: "Employees");
            migrationBuilder.DropColumn(name: "QrPinFailedAttempts", table: "Employees");
            migrationBuilder.DropColumn(name: "QrPinLockedUntil", table: "Employees");
        }
    }
}
