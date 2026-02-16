using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGeoTrackerIDToEmployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GeoTrackerID",
                table: "Employees",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId_GeoTrackerID",
                table: "Employees",
                columns: new[] { "TenantId", "GeoTrackerID" },
                unique: true,
                filter: "\"GeoTrackerID\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_TenantId_GeoTrackerID",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "GeoTrackerID",
                table: "Employees");
        }
    }
}
