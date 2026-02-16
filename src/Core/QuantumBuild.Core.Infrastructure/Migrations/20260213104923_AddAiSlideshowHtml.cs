using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiSlideshowHtml : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SlideshowGeneratedAt",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlideshowHtml",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ToolboxTalkSlideshowTranslations",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolboxTalkId = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TranslatedHtml = table.Column<string>(type: "text", nullable: false),
                    TranslatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolboxTalkSlideshowTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolboxTalkSlideshowTranslations_ToolboxTalks_ToolboxTalkId",
                        column: x => x.ToolboxTalkId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_slideshow_translations_talk_language",
                schema: "toolbox_talks",
                table: "ToolboxTalkSlideshowTranslations",
                columns: new[] { "ToolboxTalkId", "LanguageCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ToolboxTalkSlideshowTranslations",
                schema: "toolbox_talks");

            migrationBuilder.DropColumn(
                name: "SlideshowGeneratedAt",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "SlideshowHtml",
                schema: "toolbox_talks",
                table: "ToolboxTalks");
        }
    }
}
