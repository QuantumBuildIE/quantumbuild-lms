using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAudienceRoleToContentCreationSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AudienceRole",
                schema: "toolbox_talks",
                table: "ContentCreationSessions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Operator");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudienceRole",
                schema: "toolbox_talks",
                table: "ContentCreationSessions");
        }
    }
}
