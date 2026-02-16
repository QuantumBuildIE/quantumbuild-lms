using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoAssignFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AutoAssignDueDays",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "integer",
                nullable: false,
                defaultValue: 14);

            migrationBuilder.AddColumn<bool>(
                name: "AutoAssignToNewEmployees",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AutoAssignDueDays",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourses",
                type: "integer",
                nullable: false,
                defaultValue: 14);

            migrationBuilder.AddColumn<bool>(
                name: "AutoAssignToNewEmployees",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourses",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoAssignDueDays",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "AutoAssignToNewEmployees",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "AutoAssignDueDays",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourses");

            migrationBuilder.DropColumn(
                name: "AutoAssignToNewEmployees",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourses");
        }
    }
}
