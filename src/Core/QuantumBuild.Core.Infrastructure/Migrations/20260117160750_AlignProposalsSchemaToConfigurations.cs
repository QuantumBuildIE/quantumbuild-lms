using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignProposalsSchemaToConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProposalContacts_Proposals_ProposalId",
                table: "ProposalContacts");

            migrationBuilder.DropForeignKey(
                name: "FK_ProposalLineItems_ProposalSections_ProposalSectionId",
                table: "ProposalLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Proposals_Proposals_ParentProposalId",
                table: "Proposals");

            migrationBuilder.DropForeignKey(
                name: "FK_ProposalSections_Proposals_ProposalId",
                table: "ProposalSections");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Proposals",
                table: "Proposals");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProposalSections",
                table: "ProposalSections");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProposalLineItems",
                table: "ProposalLineItems");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProposalContacts",
                table: "ProposalContacts");

            migrationBuilder.RenameTable(
                name: "Proposals",
                newName: "proposals");

            migrationBuilder.RenameTable(
                name: "ProposalSections",
                newName: "proposal_sections");

            migrationBuilder.RenameTable(
                name: "ProposalLineItems",
                newName: "proposal_line_items");

            migrationBuilder.RenameTable(
                name: "ProposalContacts",
                newName: "proposal_contacts");

            migrationBuilder.RenameIndex(
                name: "IX_Proposals_ParentProposalId",
                table: "proposals",
                newName: "IX_proposals_ParentProposalId");

            migrationBuilder.RenameIndex(
                name: "IX_ProposalSections_ProposalId",
                table: "proposal_sections",
                newName: "IX_proposal_sections_ProposalId");

            migrationBuilder.RenameIndex(
                name: "IX_ProposalLineItems_ProposalSectionId",
                table: "proposal_line_items",
                newName: "IX_proposal_line_items_ProposalSectionId");

            migrationBuilder.RenameIndex(
                name: "IX_ProposalContacts_ProposalId",
                table: "proposal_contacts",
                newName: "IX_proposal_contacts_ProposalId");

            migrationBuilder.AlterColumn<string>(
                name: "WonLostReason",
                table: "proposals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "proposals",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "VatRate",
                table: "proposals",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 23m,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "VatAmount",
                table: "proposals",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                table: "proposals",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalMargin",
                table: "proposals",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalCost",
                table: "proposals",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "TermsAndConditions",
                table: "proposals",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Subtotal",
                table: "proposals",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            // Convert Status from integer enum to text string
            // 0=Draft, 1=Submitted, 2=Approved, 3=Rejected, 4=Won, 5=Lost, 6=Cancelled
            migrationBuilder.Sql(@"
                ALTER TABLE ""proposals""
                ALTER COLUMN ""Status"" TYPE text
                USING CASE ""Status""
                    WHEN 0 THEN 'Draft'
                    WHEN 1 THEN 'Submitted'
                    WHEN 2 THEN 'Approved'
                    WHEN 3 THEN 'Rejected'
                    WHEN 4 THEN 'Won'
                    WHEN 5 THEN 'Lost'
                    WHEN 6 THEN 'Cancelled'
                    ELSE 'Draft'
                END;
                ALTER TABLE ""proposals"" ALTER COLUMN ""Status"" SET DEFAULT 'Draft';
            ");

            migrationBuilder.AlterColumn<string>(
                name: "ProposalNumber",
                table: "proposals",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ProjectName",
                table: "proposals",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ProjectDescription",
                table: "proposals",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProjectAddress",
                table: "proposals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PrimaryContactName",
                table: "proposals",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PaymentTerms",
                table: "proposals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "proposals",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "NetTotal",
                table: "proposals",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "MarginPercent",
                table: "proposals",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "proposals",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<decimal>(
                name: "GrandTotal",
                table: "proposals",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "DrawingUrl",
                table: "proposals",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DrawingFileName",
                table: "proposals",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountPercent",
                table: "proposals",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountAmount",
                table: "proposals",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "proposals",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "EUR",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "proposals",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "CompanyName",
                table: "proposals",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ApprovedBy",
                table: "proposals",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                table: "proposal_sections",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SortOrder",
                table: "proposal_sections",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "SectionTotal",
                table: "proposal_sections",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "SectionName",
                table: "proposal_sections",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "SectionMargin",
                table: "proposal_sections",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "SectionCost",
                table: "proposal_sections",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "proposal_sections",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "proposal_sections",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "proposal_sections",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                table: "proposal_line_items",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "proposal_line_items",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                table: "proposal_line_items",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "Unit",
                table: "proposal_line_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Each",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "SortOrder",
                table: "proposal_line_items",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "proposal_line_items",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "ProductCode",
                table: "proposal_line_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "proposal_line_items",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "MarginPercent",
                table: "proposal_line_items",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "LineTotal",
                table: "proposal_line_items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "LineMargin",
                table: "proposal_line_items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "LineCost",
                table: "proposal_line_items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "proposal_line_items",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "proposal_line_items",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "proposal_line_items",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                table: "proposal_contacts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "proposal_contacts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "proposal_contacts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsPrimary",
                table: "proposal_contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "proposal_contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "proposal_contacts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "proposal_contacts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ContactName",
                table: "proposal_contacts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddPrimaryKey(
                name: "PK_proposals",
                table: "proposals",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_proposal_sections",
                table: "proposal_sections",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_proposal_line_items",
                table: "proposal_line_items",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_proposal_contacts",
                table: "proposal_contacts",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_proposals_CompanyId",
                table: "proposals",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_proposals_ProposalDate",
                table: "proposals",
                column: "ProposalDate");

            migrationBuilder.CreateIndex(
                name: "IX_proposals_Status",
                table: "proposals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_proposals_tenant_number",
                table: "proposals",
                columns: new[] { "TenantId", "ProposalNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_proposals_TenantId",
                table: "proposals",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_proposal_sections_ProposalId_SortOrder",
                table: "proposal_sections",
                columns: new[] { "ProposalId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_proposal_line_items_ProductId",
                table: "proposal_line_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_proposal_line_items_ProposalSectionId_SortOrder",
                table: "proposal_line_items",
                columns: new[] { "ProposalSectionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_proposal_contacts_ContactId",
                table: "proposal_contacts",
                column: "ContactId");

            migrationBuilder.AddForeignKey(
                name: "FK_proposal_contacts_proposals_ProposalId",
                table: "proposal_contacts",
                column: "ProposalId",
                principalTable: "proposals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_proposal_line_items_proposal_sections_ProposalSectionId",
                table: "proposal_line_items",
                column: "ProposalSectionId",
                principalTable: "proposal_sections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_proposal_sections_proposals_ProposalId",
                table: "proposal_sections",
                column: "ProposalId",
                principalTable: "proposals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_proposals_proposals_ParentProposalId",
                table: "proposals",
                column: "ParentProposalId",
                principalTable: "proposals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_proposal_contacts_proposals_ProposalId",
                table: "proposal_contacts");

            migrationBuilder.DropForeignKey(
                name: "FK_proposal_line_items_proposal_sections_ProposalSectionId",
                table: "proposal_line_items");

            migrationBuilder.DropForeignKey(
                name: "FK_proposal_sections_proposals_ProposalId",
                table: "proposal_sections");

            migrationBuilder.DropForeignKey(
                name: "FK_proposals_proposals_ParentProposalId",
                table: "proposals");

            migrationBuilder.DropPrimaryKey(
                name: "PK_proposals",
                table: "proposals");

            migrationBuilder.DropIndex(
                name: "IX_proposals_CompanyId",
                table: "proposals");

            migrationBuilder.DropIndex(
                name: "IX_proposals_ProposalDate",
                table: "proposals");

            migrationBuilder.DropIndex(
                name: "IX_proposals_Status",
                table: "proposals");

            migrationBuilder.DropIndex(
                name: "ix_proposals_tenant_number",
                table: "proposals");

            migrationBuilder.DropIndex(
                name: "IX_proposals_TenantId",
                table: "proposals");

            migrationBuilder.DropPrimaryKey(
                name: "PK_proposal_sections",
                table: "proposal_sections");

            migrationBuilder.DropIndex(
                name: "IX_proposal_sections_ProposalId_SortOrder",
                table: "proposal_sections");

            migrationBuilder.DropPrimaryKey(
                name: "PK_proposal_line_items",
                table: "proposal_line_items");

            migrationBuilder.DropIndex(
                name: "IX_proposal_line_items_ProductId",
                table: "proposal_line_items");

            migrationBuilder.DropIndex(
                name: "IX_proposal_line_items_ProposalSectionId_SortOrder",
                table: "proposal_line_items");

            migrationBuilder.DropPrimaryKey(
                name: "PK_proposal_contacts",
                table: "proposal_contacts");

            migrationBuilder.DropIndex(
                name: "IX_proposal_contacts_ContactId",
                table: "proposal_contacts");

            migrationBuilder.RenameTable(
                name: "proposals",
                newName: "Proposals");

            migrationBuilder.RenameTable(
                name: "proposal_sections",
                newName: "ProposalSections");

            migrationBuilder.RenameTable(
                name: "proposal_line_items",
                newName: "ProposalLineItems");

            migrationBuilder.RenameTable(
                name: "proposal_contacts",
                newName: "ProposalContacts");

            migrationBuilder.RenameIndex(
                name: "IX_proposals_ParentProposalId",
                table: "Proposals",
                newName: "IX_Proposals_ParentProposalId");

            migrationBuilder.RenameIndex(
                name: "IX_proposal_sections_ProposalId",
                table: "ProposalSections",
                newName: "IX_ProposalSections_ProposalId");

            migrationBuilder.RenameIndex(
                name: "IX_proposal_line_items_ProposalSectionId",
                table: "ProposalLineItems",
                newName: "IX_ProposalLineItems_ProposalSectionId");

            migrationBuilder.RenameIndex(
                name: "IX_proposal_contacts_ProposalId",
                table: "ProposalContacts",
                newName: "IX_ProposalContacts_ProposalId");

            migrationBuilder.AlterColumn<string>(
                name: "WonLostReason",
                table: "Proposals",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "Proposals",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.AlterColumn<decimal>(
                name: "VatRate",
                table: "Proposals",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)",
                oldPrecision: 5,
                oldScale: 2,
                oldDefaultValue: 23m);

            migrationBuilder.AlterColumn<decimal>(
                name: "VatAmount",
                table: "Proposals",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                table: "Proposals",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalMargin",
                table: "Proposals",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalCost",
                table: "Proposals",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<string>(
                name: "TermsAndConditions",
                table: "Proposals",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Subtotal",
                table: "Proposals",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            // Convert Status back from text string to integer enum
            migrationBuilder.Sql(@"
                ALTER TABLE ""Proposals"" ALTER COLUMN ""Status"" DROP DEFAULT;
                ALTER TABLE ""Proposals""
                ALTER COLUMN ""Status"" TYPE integer
                USING CASE ""Status""
                    WHEN 'Draft' THEN 0
                    WHEN 'Submitted' THEN 1
                    WHEN 'Approved' THEN 2
                    WHEN 'Rejected' THEN 3
                    WHEN 'Won' THEN 4
                    WHEN 'Lost' THEN 5
                    WHEN 'Cancelled' THEN 6
                    ELSE 0
                END;
            ");

            migrationBuilder.AlterColumn<string>(
                name: "ProposalNumber",
                table: "Proposals",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "ProjectName",
                table: "Proposals",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "ProjectDescription",
                table: "Proposals",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProjectAddress",
                table: "Proposals",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PrimaryContactName",
                table: "Proposals",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PaymentTerms",
                table: "Proposals",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "Proposals",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "NetTotal",
                table: "Proposals",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "MarginPercent",
                table: "Proposals",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)",
                oldPrecision: 5,
                oldScale: 2,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "Proposals",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<decimal>(
                name: "GrandTotal",
                table: "Proposals",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "DrawingUrl",
                table: "Proposals",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DrawingFileName",
                table: "Proposals",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountPercent",
                table: "Proposals",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)",
                oldPrecision: 5,
                oldScale: 2,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountAmount",
                table: "Proposals",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Proposals",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3,
                oldDefaultValue: "EUR");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "Proposals",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "CompanyName",
                table: "Proposals",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "ApprovedBy",
                table: "Proposals",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                table: "ProposalSections",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SortOrder",
                table: "ProposalSections",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "SectionTotal",
                table: "ProposalSections",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<string>(
                name: "SectionName",
                table: "ProposalSections",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<decimal>(
                name: "SectionMargin",
                table: "ProposalSections",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "SectionCost",
                table: "ProposalSections",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "ProposalSections",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "ProposalSections",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "ProposalSections",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                table: "ProposalLineItems",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "ProposalLineItems",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                table: "ProposalLineItems",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AlterColumn<string>(
                name: "Unit",
                table: "ProposalLineItems",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Each");

            migrationBuilder.AlterColumn<int>(
                name: "SortOrder",
                table: "ProposalLineItems",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "ProposalLineItems",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AlterColumn<string>(
                name: "ProductCode",
                table: "ProposalLineItems",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "ProposalLineItems",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "MarginPercent",
                table: "ProposalLineItems",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)",
                oldPrecision: 5,
                oldScale: 2,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "LineTotal",
                table: "ProposalLineItems",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "LineMargin",
                table: "ProposalLineItems",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "LineCost",
                table: "ProposalLineItems",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "ProposalLineItems",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "ProposalLineItems",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "ProposalLineItems",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                table: "ProposalContacts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "ProposalContacts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "ProposalContacts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsPrimary",
                table: "ProposalContacts",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "ProposalContacts",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "ProposalContacts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "ProposalContacts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "ContactName",
                table: "ProposalContacts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Proposals",
                table: "Proposals",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProposalSections",
                table: "ProposalSections",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProposalLineItems",
                table: "ProposalLineItems",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProposalContacts",
                table: "ProposalContacts",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProposalContacts_Proposals_ProposalId",
                table: "ProposalContacts",
                column: "ProposalId",
                principalTable: "Proposals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProposalLineItems_ProposalSections_ProposalSectionId",
                table: "ProposalLineItems",
                column: "ProposalSectionId",
                principalTable: "ProposalSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Proposals_Proposals_ParentProposalId",
                table: "Proposals",
                column: "ParentProposalId",
                principalTable: "Proposals",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProposalSections_Proposals_ProposalId",
                table: "ProposalSections",
                column: "ProposalId",
                principalTable: "Proposals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
