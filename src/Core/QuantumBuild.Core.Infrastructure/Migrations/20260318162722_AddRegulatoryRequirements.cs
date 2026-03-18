using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRegulatoryRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegulatoryRequirements",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegulatoryProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Section = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SectionLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Principle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PrincipleLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IngestionSource = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IngestionStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IngestionNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegulatoryRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegulatoryRequirements_RegulatoryProfiles_RegulatoryProfile~",
                        column: x => x.RegulatoryProfileId,
                        principalSchema: "toolbox_talks",
                        principalTable: "RegulatoryProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegulatoryRequirementMappings",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegulatoryRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolboxTalkId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: true),
                    MappingStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ConfidenceScore = table.Column<int>(type: "integer", nullable: true),
                    AiReasoning = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReviewedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegulatoryRequirementMappings", x => x.Id);
                    table.CheckConstraint("ck_regulatory_requirement_mappings_talk_or_course", "(\"ToolboxTalkId\" IS NOT NULL AND \"CourseId\" IS NULL) OR (\"ToolboxTalkId\" IS NULL AND \"CourseId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_RegulatoryRequirementMappings_RegulatoryRequirements_Regula~",
                        column: x => x.RegulatoryRequirementId,
                        principalSchema: "toolbox_talks",
                        principalTable: "RegulatoryRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegulatoryRequirementMappings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegulatoryRequirementMappings_ToolboxTalkCourses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalkCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegulatoryRequirementMappings_ToolboxTalks_ToolboxTalkId",
                        column: x => x.ToolboxTalkId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_regulatory_requirement_mappings_tenant_req_course",
                schema: "toolbox_talks",
                table: "RegulatoryRequirementMappings",
                columns: new[] { "TenantId", "RegulatoryRequirementId", "CourseId" },
                unique: true,
                filter: "\"CourseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_regulatory_requirement_mappings_tenant_req_talk",
                schema: "toolbox_talks",
                table: "RegulatoryRequirementMappings",
                columns: new[] { "TenantId", "RegulatoryRequirementId", "ToolboxTalkId" },
                unique: true,
                filter: "\"ToolboxTalkId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_regulatory_requirement_mappings_tenant_status",
                schema: "toolbox_talks",
                table: "RegulatoryRequirementMappings",
                columns: new[] { "TenantId", "MappingStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryRequirementMappings_CourseId",
                schema: "toolbox_talks",
                table: "RegulatoryRequirementMappings",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryRequirementMappings_RegulatoryRequirementId",
                schema: "toolbox_talks",
                table: "RegulatoryRequirementMappings",
                column: "RegulatoryRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryRequirementMappings_ToolboxTalkId",
                schema: "toolbox_talks",
                table: "RegulatoryRequirementMappings",
                column: "ToolboxTalkId");

            migrationBuilder.CreateIndex(
                name: "ix_regulatory_requirements_ingestion_status",
                schema: "toolbox_talks",
                table: "RegulatoryRequirements",
                column: "IngestionStatus");

            migrationBuilder.CreateIndex(
                name: "ix_regulatory_requirements_profile",
                schema: "toolbox_talks",
                table: "RegulatoryRequirements",
                column: "RegulatoryProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegulatoryRequirementMappings",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "RegulatoryRequirements",
                schema: "toolbox_talks");
        }
    }
}
