using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsPartOfCourseToToolboxTalks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPartOfCourse",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill existing section talks that were created as part of a course
            migrationBuilder.Sql("""
                UPDATE toolbox_talks."ToolboxTalks"
                SET "IsPartOfCourse" = true
                WHERE "Description" LIKE 'Part of course:%'
                AND "IsDeleted" = false;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPartOfCourse",
                schema: "toolbox_talks",
                table: "ToolboxTalks");
        }
    }
}
