using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationTogglesToToolboxTalkSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotifyOnExternalReviewResponse",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyOnFailure",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyOnTranslationComplete",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyOnValidationComplete",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotifyOnExternalReviewResponse",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings");

            migrationBuilder.DropColumn(
                name: "NotifyOnFailure",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings");

            migrationBuilder.DropColumn(
                name: "NotifyOnTranslationComplete",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings");

            migrationBuilder.DropColumn(
                name: "NotifyOnValidationComplete",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings");
        }
    }
}
