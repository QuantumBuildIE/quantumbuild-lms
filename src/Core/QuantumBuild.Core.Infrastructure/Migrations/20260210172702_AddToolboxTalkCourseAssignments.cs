using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddToolboxTalkCourseAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CourseAssignmentId",
                schema: "toolbox_talks",
                table: "ScheduledTalks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CourseOrderIndex",
                schema: "toolbox_talks",
                table: "ScheduledTalks",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ToolboxTalkCourseAssignments",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Assigned"),
                    IsRefresher = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    OriginalAssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolboxTalkCourseAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolboxTalkCourseAssignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ToolboxTalkCourseAssignments_ToolboxTalkCourseAssignments_O~",
                        column: x => x.OriginalAssignmentId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalkCourseAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ToolboxTalkCourseAssignments_ToolboxTalkCourses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalkCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_talks_course_assignment",
                schema: "toolbox_talks",
                table: "ScheduledTalks",
                column: "CourseAssignmentId");

            migrationBuilder.CreateIndex(
                name: "ix_course_assignments_course",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseAssignments",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "ix_course_assignments_course_employee",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseAssignments",
                columns: new[] { "CourseId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "ix_course_assignments_employee",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "ix_course_assignments_tenant",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_course_assignments_tenant_status",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseAssignments",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ToolboxTalkCourseAssignments_OriginalAssignmentId",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseAssignments",
                column: "OriginalAssignmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduledTalks_ToolboxTalkCourseAssignments_CourseAssignmen~",
                schema: "toolbox_talks",
                table: "ScheduledTalks",
                column: "CourseAssignmentId",
                principalSchema: "toolbox_talks",
                principalTable: "ToolboxTalkCourseAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduledTalks_ToolboxTalkCourseAssignments_CourseAssignmen~",
                schema: "toolbox_talks",
                table: "ScheduledTalks");

            migrationBuilder.DropTable(
                name: "ToolboxTalkCourseAssignments",
                schema: "toolbox_talks");

            migrationBuilder.DropIndex(
                name: "ix_scheduled_talks_course_assignment",
                schema: "toolbox_talks",
                table: "ScheduledTalks");

            migrationBuilder.DropColumn(
                name: "CourseAssignmentId",
                schema: "toolbox_talks",
                table: "ScheduledTalks");

            migrationBuilder.DropColumn(
                name: "CourseOrderIndex",
                schema: "toolbox_talks",
                table: "ScheduledTalks");
        }
    }
}
