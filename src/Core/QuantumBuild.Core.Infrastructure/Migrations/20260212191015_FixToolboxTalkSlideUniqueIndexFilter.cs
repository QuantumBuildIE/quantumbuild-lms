using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixToolboxTalkSlideUniqueIndexFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_toolbox_talk_slides_talk_page",
                schema: "toolbox_talks",
                table: "ToolboxTalkSlides");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_slides_talk_page",
                schema: "toolbox_talks",
                table: "ToolboxTalkSlides",
                columns: new[] { "ToolboxTalkId", "PageNumber" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_toolbox_talk_slides_talk_page",
                schema: "toolbox_talks",
                table: "ToolboxTalkSlides");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_slides_talk_page",
                schema: "toolbox_talks",
                table: "ToolboxTalkSlides",
                columns: new[] { "ToolboxTalkId", "PageNumber" },
                unique: true);
        }
    }
}
