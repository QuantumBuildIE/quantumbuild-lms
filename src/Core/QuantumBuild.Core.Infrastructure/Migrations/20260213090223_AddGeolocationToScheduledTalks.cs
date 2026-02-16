using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGeolocationToScheduledTalks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "StartedAccuracyMeters",
                schema: "toolbox_talks",
                table: "ScheduledTalks",
                type: "double precision",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "StartedLatitude",
                schema: "toolbox_talks",
                table: "ScheduledTalks",
                type: "double precision",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedLocationTimestamp",
                schema: "toolbox_talks",
                table: "ScheduledTalks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "StartedLongitude",
                schema: "toolbox_talks",
                table: "ScheduledTalks",
                type: "double precision",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CompletedAccuracyMeters",
                schema: "toolbox_talks",
                table: "ScheduledTalkCompletions",
                type: "double precision",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CompletedLatitude",
                schema: "toolbox_talks",
                table: "ScheduledTalkCompletions",
                type: "double precision",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedLocationTimestamp",
                schema: "toolbox_talks",
                table: "ScheduledTalkCompletions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CompletedLongitude",
                schema: "toolbox_talks",
                table: "ScheduledTalkCompletions",
                type: "double precision",
                precision: 9,
                scale: 6,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartedAccuracyMeters",
                schema: "toolbox_talks",
                table: "ScheduledTalks");

            migrationBuilder.DropColumn(
                name: "StartedLatitude",
                schema: "toolbox_talks",
                table: "ScheduledTalks");

            migrationBuilder.DropColumn(
                name: "StartedLocationTimestamp",
                schema: "toolbox_talks",
                table: "ScheduledTalks");

            migrationBuilder.DropColumn(
                name: "StartedLongitude",
                schema: "toolbox_talks",
                table: "ScheduledTalks");

            migrationBuilder.DropColumn(
                name: "CompletedAccuracyMeters",
                schema: "toolbox_talks",
                table: "ScheduledTalkCompletions");

            migrationBuilder.DropColumn(
                name: "CompletedLatitude",
                schema: "toolbox_talks",
                table: "ScheduledTalkCompletions");

            migrationBuilder.DropColumn(
                name: "CompletedLocationTimestamp",
                schema: "toolbox_talks",
                table: "ScheduledTalkCompletions");

            migrationBuilder.DropColumn(
                name: "CompletedLongitude",
                schema: "toolbox_talks",
                table: "ScheduledTalkCompletions");
        }
    }
}
