using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQrCodeCourseId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QrPin",
                table: "Employees",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QrPinFailedAttempts",
                table: "Employees",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "QrPinGeneratedAt",
                table: "Employees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "QrPinIsSet",
                table: "Employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "QrPinLastUsedAt",
                table: "Employees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "QrPinLockedUntil",
                table: "Employees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "QrLocations",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QrLocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QrCodes",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QrLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolboxTalkId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentMode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CodeToken = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    QrImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QrCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QrCodes_QrLocations_QrLocationId",
                        column: x => x.QrLocationId,
                        principalSchema: "toolbox_talks",
                        principalTable: "QrLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QrCodes_ToolboxTalkCourses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalkCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_QrCodes_ToolboxTalks_ToolboxTalkId",
                        column: x => x.ToolboxTalkId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "QrSessions",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    QrCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionToken = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ContentMode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SignedOffAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Active"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QrSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QrSessions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QrSessions_QrCodes_QrCodeId",
                        column: x => x.QrCodeId,
                        principalSchema: "toolbox_talks",
                        principalTable: "QrCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_qr_codes_tenant",
                schema: "toolbox_talks",
                table: "QrCodes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_qr_codes_token",
                schema: "toolbox_talks",
                table: "QrCodes",
                column: "CodeToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QrCodes_CourseId",
                schema: "toolbox_talks",
                table: "QrCodes",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_QrCodes_QrLocationId",
                schema: "toolbox_talks",
                table: "QrCodes",
                column: "QrLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_QrCodes_ToolboxTalkId",
                schema: "toolbox_talks",
                table: "QrCodes",
                column: "ToolboxTalkId");

            migrationBuilder.CreateIndex(
                name: "ix_qr_locations_tenant",
                schema: "toolbox_talks",
                table: "QrLocations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_qr_sessions_tenant",
                schema: "toolbox_talks",
                table: "QrSessions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_qr_sessions_token",
                schema: "toolbox_talks",
                table: "QrSessions",
                column: "SessionToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QrSessions_EmployeeId",
                schema: "toolbox_talks",
                table: "QrSessions",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_QrSessions_QrCodeId",
                schema: "toolbox_talks",
                table: "QrSessions",
                column: "QrCodeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QrSessions",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "QrCodes",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "QrLocations",
                schema: "toolbox_talks");

            migrationBuilder.DropColumn(
                name: "QrPin",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "QrPinFailedAttempts",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "QrPinGeneratedAt",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "QrPinIsSet",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "QrPinLastUsedAt",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "QrPinLockedUntil",
                table: "Employees");
        }
    }
}
