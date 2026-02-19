using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupervisorAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupervisorAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupervisorEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperatorEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupervisorAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupervisorAssignments_Employees_OperatorEmployeeId",
                        column: x => x.OperatorEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupervisorAssignments_Employees_SupervisorEmployeeId",
                        column: x => x.SupervisorEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupervisorAssignments_OperatorEmployeeId",
                table: "SupervisorAssignments",
                column: "OperatorEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_SupervisorAssignments_SupervisorEmployeeId",
                table: "SupervisorAssignments",
                column: "SupervisorEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_SupervisorAssignments_TenantId_SupervisorEmployeeId_OperatorEmployeeId",
                table: "SupervisorAssignments",
                columns: new[] { "TenantId", "SupervisorEmployeeId", "OperatorEmployeeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupervisorAssignments");
        }
    }
}
