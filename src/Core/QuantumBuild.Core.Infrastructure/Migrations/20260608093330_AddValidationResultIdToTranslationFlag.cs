using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddValidationResultIdToTranslationFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ValidationResultId",
                schema: "toolbox_talks",
                table: "TranslationFlags",
                type: "uuid",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "ix_translation_flags_validation_result",
                schema: "toolbox_talks",
                table: "TranslationFlags",
                column: "ValidationResultId");

            migrationBuilder.AddForeignKey(
                name: "FK_TranslationFlags_TranslationValidationResults_ValidationRes~",
                schema: "toolbox_talks",
                table: "TranslationFlags",
                column: "ValidationResultId",
                principalSchema: "toolbox_talks",
                principalTable: "TranslationValidationResults",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TranslationFlags_TranslationValidationResults_ValidationRes~",
                schema: "toolbox_talks",
                table: "TranslationFlags");

            migrationBuilder.DropIndex(
                name: "ix_translation_flags_validation_result",
                schema: "toolbox_talks",
                table: "TranslationFlags");

            migrationBuilder.DropColumn(
                name: "ValidationResultId",
                schema: "toolbox_talks",
                table: "TranslationFlags");
        }
    }
}
