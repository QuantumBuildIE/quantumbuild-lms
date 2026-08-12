using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleDepartmentSiteTargeting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<Guid>>(
                name: "TargetDepartmentIds",
                schema: "toolbox_talks",
                table: "ToolboxTalkSchedules",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<Guid>>(
                name: "TargetSiteIds",
                schema: "toolbox_talks",
                table: "ToolboxTalkSchedules",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<bool>(
                name: "IsCriteriaDerived",
                schema: "toolbox_talks",
                table: "ToolboxTalkScheduleAssignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetDepartmentIds",
                schema: "toolbox_talks",
                table: "ToolboxTalkSchedules");

            migrationBuilder.DropColumn(
                name: "TargetSiteIds",
                schema: "toolbox_talks",
                table: "ToolboxTalkSchedules");

            migrationBuilder.DropColumn(
                name: "IsCriteriaDerived",
                schema: "toolbox_talks",
                table: "ToolboxTalkScheduleAssignments");
        }
    }
}
