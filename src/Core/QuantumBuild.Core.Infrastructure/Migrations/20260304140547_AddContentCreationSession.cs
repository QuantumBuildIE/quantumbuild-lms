using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContentCreationSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentCreationSessions",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InputMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Draft"),
                    SourceText = table.Column<string>(type: "text", nullable: true),
                    SourceFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SourceFileUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SourceFileType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TranscriptText = table.Column<string>(type: "text", nullable: true),
                    ParsedSectionsJson = table.Column<string>(type: "text", nullable: true),
                    OutputType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    OutputId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetLanguageCodes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PassThreshold = table.Column<int>(type: "integer", nullable: false, defaultValue: 75),
                    SectorKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReviewerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReviewerOrg = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReviewerRole = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DocumentRef = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AuditPurpose = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidationRunIds = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentCreationSessions", x => x.Id);
                    table.ForeignKey(
                        name: "fk_content_creation_sessions_talk",
                        column: x => x.OutputId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_content_creation_sessions_expires",
                schema: "toolbox_talks",
                table: "ContentCreationSessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "ix_content_creation_sessions_status",
                schema: "toolbox_talks",
                table: "ContentCreationSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_content_creation_sessions_tenant",
                schema: "toolbox_talks",
                table: "ContentCreationSessions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_content_creation_sessions_tenant_status",
                schema: "toolbox_talks",
                table: "ContentCreationSessions",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentCreationSessions_OutputId",
                schema: "toolbox_talks",
                table: "ContentCreationSessions",
                column: "OutputId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentCreationSessions",
                schema: "toolbox_talks");
        }
    }
}
