using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantReviewerConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantReviewerConfigurations",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ReviewerEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ReviewerName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
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
                    table.PrimaryKey("PK_TenantReviewerConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantReviewerConfigurations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_reviewer_configurations_tenant_fallback",
                schema: "toolbox_talks",
                table: "TenantReviewerConfigurations",
                column: "TenantId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"LanguageCode\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_reviewer_configurations_tenant_language",
                schema: "toolbox_talks",
                table: "TenantReviewerConfigurations",
                columns: new[] { "TenantId", "LanguageCode" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"LanguageCode\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantReviewerConfigurations",
                schema: "toolbox_talks");
        }
    }
}
