using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQrCodeCourseId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Pin columns are added by AddEmployeePinFields (20260424160000).
            // QrLocations / QrCodes / QrSessions tables are created by AddQrLocationAndQrCode (20260424200000)
            // and AddQrSession (20260424210000). This migration only adds the CourseId column.
            migrationBuilder.AddColumn<Guid>(
                name: "CourseId",
                schema: "toolbox_talks",
                table: "QrCodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_QrCodes_ToolboxTalkCourses_CourseId",
                schema: "toolbox_talks",
                table: "QrCodes",
                column: "CourseId",
                principalSchema: "toolbox_talks",
                principalTable: "ToolboxTalkCourses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.CreateIndex(
                name: "IX_QrCodes_CourseId",
                schema: "toolbox_talks",
                table: "QrCodes",
                column: "CourseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QrCodes_ToolboxTalkCourses_CourseId",
                schema: "toolbox_talks",
                table: "QrCodes");

            migrationBuilder.DropIndex(
                name: "IX_QrCodes_CourseId",
                schema: "toolbox_talks",
                table: "QrCodes");

            migrationBuilder.DropColumn(
                name: "CourseId",
                schema: "toolbox_talks",
                table: "QrCodes");
        }
    }
}
