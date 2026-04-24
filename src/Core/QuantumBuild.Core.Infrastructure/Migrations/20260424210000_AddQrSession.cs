#nullable disable

using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace QuantumBuild.Core.Infrastructure.Migrations;

[Migration("20260424210000_AddQrSession")]
public class AddQrSession : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
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
                Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Active"),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                SignedOffAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Score = table.Column<int>(type: "integer", nullable: true),
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
            name: "ix_qr_sessions_token",
            schema: "toolbox_talks",
            table: "QrSessions",
            column: "SessionToken",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_qr_sessions_tenant",
            schema: "toolbox_talks",
            table: "QrSessions",
            column: "TenantId");

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

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "QrSessions", schema: "toolbox_talks");
    }
}
