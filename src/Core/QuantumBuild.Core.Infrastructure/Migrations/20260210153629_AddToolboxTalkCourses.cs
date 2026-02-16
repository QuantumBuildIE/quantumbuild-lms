using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddToolboxTalkCourses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ToolboxTalkCourses",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    RequireSequentialCompletion = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    RequiresRefresher = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    RefresherIntervalMonths = table.Column<int>(type: "integer", nullable: false, defaultValue: 12),
                    GenerateCertificate = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolboxTalkCourses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ToolboxTalkCourseItems",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolboxTalkId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolboxTalkCourseItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolboxTalkCourseItems_ToolboxTalkCourses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalkCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ToolboxTalkCourseItems_ToolboxTalks_ToolboxTalkId",
                        column: x => x.ToolboxTalkId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ToolboxTalkCourseTranslations",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TranslatedTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TranslatedDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolboxTalkCourseTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolboxTalkCourseTranslations_ToolboxTalkCourses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalkCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_course_items_course",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseItems",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_course_items_course_order",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseItems",
                columns: new[] { "CourseId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_course_items_course_talk",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseItems",
                columns: new[] { "CourseId", "ToolboxTalkId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ToolboxTalkCourseItems_ToolboxTalkId",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseItems",
                column: "ToolboxTalkId");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_courses_tenant",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_courses_tenant_active",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourses",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_course_translations_course",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseTranslations",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_course_translations_course_language",
                schema: "toolbox_talks",
                table: "ToolboxTalkCourseTranslations",
                columns: new[] { "CourseId", "LanguageCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ToolboxTalkCourseItems",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "ToolboxTalkCourseTranslations",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "ToolboxTalkCourses",
                schema: "toolbox_talks");
        }
    }
}
