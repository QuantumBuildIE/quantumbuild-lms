using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRefresherSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RefresherIntervalMonths",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "integer",
                nullable: false,
                defaultValue: 12);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresRefresher",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefresherDueDate",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseAssignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReminderSent1Week",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseAssignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReminderSent2Weeks",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseAssignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRefresher",
                schema: "toolbox_talks",
                table: "ScheduledTalks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalScheduledTalkId",
                schema: "toolbox_talks",
                table: "ScheduledTalks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefresherDueDate",
                schema: "toolbox_talks",
                table: "ScheduledTalks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReminderSent1Week",
                schema: "toolbox_talks",
                table: "ScheduledTalks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReminderSent2Weeks",
                schema: "toolbox_talks",
                table: "ScheduledTalks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_talks_original",
                schema: "toolbox_talks",
                table: "ScheduledTalks",
                column: "OriginalScheduledTalkId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduledTalks_ScheduledTalks_OriginalScheduledTalkId",
                schema: "toolbox_talks",
                table: "ScheduledTalks",
                column: "OriginalScheduledTalkId",
                principalSchema: "toolbox_talks",
                principalTable: "ScheduledTalks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduledTalks_ScheduledTalks_OriginalScheduledTalkId",
                schema: "toolbox_talks",
                table: "ScheduledTalks");

            migrationBuilder.DropIndex(
                name: "ix_scheduled_talks_original",
                schema: "toolbox_talks",
                table: "ScheduledTalks");

            migrationBuilder.DropColumn(
                name: "RefresherIntervalMonths",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "RequiresRefresher",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "RefresherDueDate",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseAssignments");

            migrationBuilder.DropColumn(
                name: "ReminderSent1Week",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseAssignments");

            migrationBuilder.DropColumn(
                name: "ReminderSent2Weeks",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseAssignments");

            migrationBuilder.DropColumn(
                name: "IsRefresher",
                schema: "toolbox_talks",
                table: "ScheduledTalks");

            migrationBuilder.DropColumn(
                name: "OriginalScheduledTalkId",
                schema: "toolbox_talks",
                table: "ScheduledTalks");

            migrationBuilder.DropColumn(
                name: "RefresherDueDate",
                schema: "toolbox_talks",
                table: "ScheduledTalks");

            migrationBuilder.DropColumn(
                name: "ReminderSent1Week",
                schema: "toolbox_talks",
                table: "ScheduledTalks");

            migrationBuilder.DropColumn(
                name: "ReminderSent2Weeks",
                schema: "toolbox_talks",
                table: "ScheduledTalks");
        }
    }
}
