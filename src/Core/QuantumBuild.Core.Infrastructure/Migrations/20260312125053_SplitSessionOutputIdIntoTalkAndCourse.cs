using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitSessionOutputIdIntoTalkAndCourse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OutputId",
                schema: "toolbox_talks",
                table: "ContentCreationSessions",
                newName: "OutputTalkId");

            migrationBuilder.RenameIndex(
                name: "IX_ContentCreationSessions_OutputId",
                schema: "toolbox_talks",
                table: "ContentCreationSessions",
                newName: "IX_ContentCreationSessions_OutputTalkId");

            migrationBuilder.AddColumn<Guid>(
                name: "OutputCourseId",
                schema: "toolbox_talks",
                table: "ContentCreationSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentCreationSessions_OutputCourseId",
                schema: "toolbox_talks",
                table: "ContentCreationSessions",
                column: "OutputCourseId");

            migrationBuilder.AddForeignKey(
                name: "fk_content_creation_sessions_course",
                schema: "toolbox_talks",
                table: "ContentCreationSessions",
                column: "OutputCourseId",
                principalSchema: "toolbox_talks",
                principalTable: "ToolboxTalkCourses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_content_creation_sessions_course",
                schema: "toolbox_talks",
                table: "ContentCreationSessions");

            migrationBuilder.DropIndex(
                name: "IX_ContentCreationSessions_OutputCourseId",
                schema: "toolbox_talks",
                table: "ContentCreationSessions");

            migrationBuilder.DropColumn(
                name: "OutputCourseId",
                schema: "toolbox_talks",
                table: "ContentCreationSessions");

            migrationBuilder.RenameColumn(
                name: "OutputTalkId",
                schema: "toolbox_talks",
                table: "ContentCreationSessions",
                newName: "OutputId");

            migrationBuilder.RenameIndex(
                name: "IX_ContentCreationSessions_OutputTalkId",
                schema: "toolbox_talks",
                table: "ContentCreationSessions",
                newName: "IX_ContentCreationSessions_OutputId");
        }
    }
}
