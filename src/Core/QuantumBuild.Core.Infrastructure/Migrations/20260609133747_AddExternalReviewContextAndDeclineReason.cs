using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalReviewContextAndDeclineReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeclineReason",
                schema: "workflows",
                table: "WorkflowReviews",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContextPayload",
                schema: "workflows",
                table: "ExternalParticipantInvitations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContextType",
                schema: "workflows",
                table: "ExternalParticipantInvitations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeclineReason",
                schema: "workflows",
                table: "WorkflowReviews");

            migrationBuilder.DropColumn(
                name: "ContextPayload",
                schema: "workflows",
                table: "ExternalParticipantInvitations");

            migrationBuilder.DropColumn(
                name: "ContextType",
                schema: "workflows",
                table: "ExternalParticipantInvitations");
        }
    }
}
