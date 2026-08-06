using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateEmailFailedAndCourseGenerationFailed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CertificateGenerationFailed",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseAssignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CertificateEmailFailed",
                schema: "toolbox_talks",
                table: "ToolboxTalkCertificates",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CertificateGenerationFailed",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseAssignments");

            migrationBuilder.DropColumn(
                name: "CertificateEmailFailed",
                schema: "toolbox_talks",
                table: "ToolboxTalkCertificates");
        }
    }
}
