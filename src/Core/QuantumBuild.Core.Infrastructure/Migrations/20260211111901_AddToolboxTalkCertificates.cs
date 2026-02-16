using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddToolboxTalkCertificates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ToolboxTalkCertificates",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ToolboxTalkId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScheduledTalkId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseAssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CertificateNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PdfStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsRefresher = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    EmployeeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EmployeeCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TrainingTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IncludedTalksJson = table.Column<string>(type: "text", nullable: true),
                    SignatureDataUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolboxTalkCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolboxTalkCertificates_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ToolboxTalkCertificates_ToolboxTalkCourses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalkCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ToolboxTalkCertificates_ToolboxTalks_ToolboxTalkId",
                        column: x => x.ToolboxTalkId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_certificates_employee",
                schema: "toolbox_talks",
                table: "ToolboxTalkCertificates",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_certificates_number",
                schema: "toolbox_talks",
                table: "ToolboxTalkCertificates",
                column: "CertificateNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_certificates_tenant",
                schema: "toolbox_talks",
                table: "ToolboxTalkCertificates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_certificates_tenant_employee",
                schema: "toolbox_talks",
                table: "ToolboxTalkCertificates",
                columns: new[] { "TenantId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_ToolboxTalkCertificates_CourseId",
                schema: "toolbox_talks",
                table: "ToolboxTalkCertificates",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolboxTalkCertificates_ToolboxTalkId",
                schema: "toolbox_talks",
                table: "ToolboxTalkCertificates",
                column: "ToolboxTalkId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ToolboxTalkCertificates",
                schema: "toolbox_talks");
        }
    }
}
