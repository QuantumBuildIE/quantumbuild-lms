using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWizardDefaultsToToolboxTalkSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefaultAutoAssignDueDays",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings",
                type: "integer",
                nullable: false,
                defaultValue: 14);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultGenerateCertificate",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultIsActive",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultMinimumVideoWatchPercent",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings",
                type: "integer",
                nullable: false,
                defaultValue: 90);

            migrationBuilder.AddColumn<string>(
                name: "DefaultRefresherFrequency",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Once");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultAutoAssignDueDays",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings");

            migrationBuilder.DropColumn(
                name: "DefaultGenerateCertificate",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings");

            migrationBuilder.DropColumn(
                name: "DefaultIsActive",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings");

            migrationBuilder.DropColumn(
                name: "DefaultMinimumVideoWatchPercent",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings");

            migrationBuilder.DropColumn(
                name: "DefaultRefresherFrequency",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings");
        }
    }
}
