using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Modules.LessonParser.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonParserModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "lesson_parser");

            migrationBuilder.CreateTable(
                name: "ParseJobs",
                schema: "lesson_parser",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InputType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InputReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Processing"),
                    GeneratedCourseId = table.Column<Guid>(type: "uuid", nullable: true),
                    GeneratedCourseTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TalksGenerated = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParseJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_parse_jobs_tenant",
                schema: "lesson_parser",
                table: "ParseJobs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_parse_jobs_tenant_status",
                schema: "lesson_parser",
                table: "ParseJobs",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParseJobs",
                schema: "lesson_parser");
        }
    }
}
