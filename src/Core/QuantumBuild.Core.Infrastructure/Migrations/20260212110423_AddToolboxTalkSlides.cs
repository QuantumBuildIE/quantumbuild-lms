using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddToolboxTalkSlides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "GenerateSlidesFromPdf",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SlidesGenerated",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ToolboxTalkSlides",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolboxTalkId = table.Column<Guid>(type: "uuid", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: false),
                    ImageStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OriginalText = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolboxTalkSlides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolboxTalkSlides_ToolboxTalks_ToolboxTalkId",
                        column: x => x.ToolboxTalkId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ToolboxTalkSlideTranslations",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SlideId = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TranslatedText = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolboxTalkSlideTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolboxTalkSlideTranslations_ToolboxTalkSlides_SlideId",
                        column: x => x.SlideId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalkSlides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_slides_talk",
                schema: "toolbox_talks",
                table: "ToolboxTalkSlides",
                column: "ToolboxTalkId");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_slides_talk_page",
                schema: "toolbox_talks",
                table: "ToolboxTalkSlides",
                columns: new[] { "ToolboxTalkId", "PageNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_slide_translations_slide_language",
                schema: "toolbox_talks",
                table: "ToolboxTalkSlideTranslations",
                columns: new[] { "SlideId", "LanguageCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ToolboxTalkSlideTranslations",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "ToolboxTalkSlides",
                schema: "toolbox_talks");

            migrationBuilder.DropColumn(
                name: "GenerateSlidesFromPdf",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "SlidesGenerated",
                schema: "toolbox_talks",
                table: "ToolboxTalks");
        }
    }
}
