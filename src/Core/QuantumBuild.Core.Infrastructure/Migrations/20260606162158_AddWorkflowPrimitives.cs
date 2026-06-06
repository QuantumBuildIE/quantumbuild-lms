using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowPrimitives : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "workflows");

            migrationBuilder.CreateTable(
                name: "ExternalParticipantInvitations",
                schema: "workflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowType = table.Column<int>(type: "integer", nullable: false),
                    TargetEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetEntitySubKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    InvitedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequesterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_ExternalParticipantInvitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TranslationFlags",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolboxTalkId = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    StartOffset = table.Column<int>(type: "integer", nullable: false),
                    EndOffset = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
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
                    table.PrimaryKey("PK_TranslationFlags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TranslationFlags_ToolboxTalks_ToolboxTalkId",
                        column: x => x.ToolboxTalkId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowEvents",
                schema: "workflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowType = table.Column<int>(type: "integer", nullable: false),
                    TargetEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetEntitySubKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    EventType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TriggeredByType = table.Column<int>(type: "integer", nullable: false),
                    TriggeredByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_WorkflowEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowReviews",
                schema: "workflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowType = table.Column<int>(type: "integer", nullable: false),
                    TargetEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetEntitySubKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ReviewerType = table.Column<int>(type: "integer", nullable: false),
                    ReviewerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalParticipantInvitationId = table.Column<Guid>(type: "uuid", nullable: true),
                    EditedContent = table.Column<string>(type: "text", nullable: true),
                    Accepted = table.Column<bool>(type: "boolean", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_WorkflowReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowReviews_ExternalParticipantInvitations_ExternalPart~",
                        column: x => x.ExternalParticipantInvitationId,
                        principalSchema: "workflows",
                        principalTable: "ExternalParticipantInvitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_external_participant_invitations_target",
                schema: "workflows",
                table: "ExternalParticipantInvitations",
                columns: new[] { "WorkflowType", "TargetEntityId", "TargetEntitySubKey" });

            migrationBuilder.CreateIndex(
                name: "ix_external_participant_invitations_tenant",
                schema: "workflows",
                table: "ExternalParticipantInvitations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_external_participant_invitations_token_hash",
                schema: "workflows",
                table: "ExternalParticipantInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_translation_flags_talk_language",
                schema: "toolbox_talks",
                table: "TranslationFlags",
                columns: new[] { "ToolboxTalkId", "LanguageCode" });

            migrationBuilder.CreateIndex(
                name: "ix_translation_flags_tenant",
                schema: "toolbox_talks",
                table: "TranslationFlags",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_events_target",
                schema: "workflows",
                table: "WorkflowEvents",
                columns: new[] { "WorkflowType", "TargetEntityId", "TargetEntitySubKey" });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_events_tenant",
                schema: "workflows",
                table: "WorkflowEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_reviews_target",
                schema: "workflows",
                table: "WorkflowReviews",
                columns: new[] { "WorkflowType", "TargetEntityId", "TargetEntitySubKey" });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_reviews_tenant",
                schema: "workflows",
                table: "WorkflowReviews",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowReviews_ExternalParticipantInvitationId",
                schema: "workflows",
                table: "WorkflowReviews",
                column: "ExternalParticipantInvitationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TranslationFlags",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "WorkflowEvents",
                schema: "workflows");

            migrationBuilder.DropTable(
                name: "WorkflowReviews",
                schema: "workflows");

            migrationBuilder.DropTable(
                name: "ExternalParticipantInvitations",
                schema: "workflows");
        }
    }
}
