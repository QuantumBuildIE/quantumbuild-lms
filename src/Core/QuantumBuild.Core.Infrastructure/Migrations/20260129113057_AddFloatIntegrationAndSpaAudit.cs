using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFloatIntegrationAndSpaAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FloatLinkMethod",
                table: "Sites",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FloatLinkedAt",
                table: "Sites",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FloatProjectId",
                table: "Sites",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FloatLinkMethod",
                table: "Employees",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FloatLinkedAt",
                table: "Employees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FloatPersonId",
                table: "Employees",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "float_unmatched_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FloatId = table.Column<int>(type: "integer", nullable: false),
                    FloatName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FloatEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SuggestedMatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    SuggestedMatchName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MatchConfidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LinkedToId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_float_unmatched_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "spa_notification_audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    FloatTaskId = table.Column<int>(type: "integer", nullable: true),
                    FloatPersonId = table.Column<int>(type: "integer", nullable: true),
                    FloatProjectId = table.Column<int>(type: "integer", nullable: true),
                    ScheduledDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ScheduledHours = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    NotificationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NotificationMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EmailProviderId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SpaSubmitted = table.Column<bool>(type: "boolean", nullable: false),
                    SpaId = table.Column<Guid>(type: "uuid", nullable: true),
                    SpaSubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spa_notification_audit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_spa_notification_audit_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_spa_notification_audit_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sites_FloatProjectId",
                table: "Sites",
                column: "FloatProjectId",
                filter: "\"FloatProjectId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_FloatPersonId",
                table: "Employees",
                column: "FloatPersonId",
                filter: "\"FloatPersonId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FloatUnmatchedItems_TenantId_ItemType_FloatId",
                table: "float_unmatched_items",
                columns: new[] { "TenantId", "ItemType", "FloatId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FloatUnmatchedItems_TenantId_Status",
                table: "float_unmatched_items",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_spa_notification_audit_EmployeeId",
                table: "spa_notification_audit",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_spa_notification_audit_SiteId",
                table: "spa_notification_audit",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_SpaNotificationAudit_CreatedAt",
                table: "spa_notification_audit",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SpaNotificationAudit_TenantId_EmployeeId_ScheduledDate",
                table: "spa_notification_audit",
                columns: new[] { "TenantId", "EmployeeId", "ScheduledDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SpaNotificationAudit_TenantId_ScheduledDate",
                table: "spa_notification_audit",
                columns: new[] { "TenantId", "ScheduledDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SpaNotificationAudit_TenantId_Status",
                table: "spa_notification_audit",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "float_unmatched_items");

            migrationBuilder.DropTable(
                name: "spa_notification_audit");

            migrationBuilder.DropIndex(
                name: "IX_Sites_FloatProjectId",
                table: "Sites");

            migrationBuilder.DropIndex(
                name: "IX_Employees_FloatPersonId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "FloatLinkMethod",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "FloatLinkedAt",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "FloatProjectId",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "FloatLinkMethod",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "FloatLinkedAt",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "FloatPersonId",
                table: "Employees");
        }
    }
}
