using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSingleSupervisorUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SupervisorAssignments_TenantId_OperatorEmployeeId_Active",
                table: "SupervisorAssignments",
                columns: new[] { "TenantId", "OperatorEmployeeId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SupervisorAssignments_TenantId_OperatorEmployeeId_Active",
                table: "SupervisorAssignments");
        }
    }
}
