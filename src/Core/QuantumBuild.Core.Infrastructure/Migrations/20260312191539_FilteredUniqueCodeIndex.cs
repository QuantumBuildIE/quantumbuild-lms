using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FilteredUniqueCodeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ToolboxTalks_TenantId_Code",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.CreateIndex(
                name: "IX_ToolboxTalks_TenantId_Code",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ToolboxTalks_TenantId_Code",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.CreateIndex(
                name: "IX_ToolboxTalks_TenantId_Code",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                columns: new[] { "TenantId", "Code" },
                unique: true);
        }
    }
}
