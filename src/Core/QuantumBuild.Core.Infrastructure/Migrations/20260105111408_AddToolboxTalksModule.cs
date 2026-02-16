using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddToolboxTalksModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "toolbox_talks");

            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "Employees",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.CreateTable(
                name: "ToolboxTalks",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Frequency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Once"),
                    VideoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    VideoSource = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "None"),
                    AttachmentUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MinimumVideoWatchPercent = table.Column<int>(type: "integer", nullable: false, defaultValue: 90),
                    RequiresQuiz = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PassingScore = table.Column<int>(type: "integer", nullable: true, defaultValue: 80),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolboxTalks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ToolboxTalkSettings",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultDueDays = table.Column<int>(type: "integer", nullable: false, defaultValue: 7),
                    ReminderFrequencyDays = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    MaxReminders = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    EscalateAfterReminders = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    RequireVideoCompletion = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DefaultPassingScore = table.Column<int>(type: "integer", nullable: false, defaultValue: 80),
                    EnableTranslation = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TranslationProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EnableVideoDubbing = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    VideoDubbingProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NotificationEmailTemplate = table.Column<string>(type: "text", nullable: true),
                    ReminderEmailTemplate = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolboxTalkSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ToolboxTalkQuestions",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolboxTalkId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionNumber = table.Column<int>(type: "integer", nullable: false),
                    QuestionText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    QuestionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "MultipleChoice"),
                    Options = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CorrectAnswer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolboxTalkQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolboxTalkQuestions_ToolboxTalks_ToolboxTalkId",
                        column: x => x.ToolboxTalkId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ToolboxTalkSchedules",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolboxTalkId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Frequency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Once"),
                    AssignToAllEmployees = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Draft"),
                    NextRunDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolboxTalkSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolboxTalkSchedules_ToolboxTalks_ToolboxTalkId",
                        column: x => x.ToolboxTalkId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ToolboxTalkSections",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolboxTalkId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    RequiresAcknowledgment = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolboxTalkSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolboxTalkSections_ToolboxTalks_ToolboxTalkId",
                        column: x => x.ToolboxTalkId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ToolboxTalkTranslations",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolboxTalkId = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TranslatedTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TranslatedDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TranslatedSections = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    TranslatedQuestions = table.Column<string>(type: "text", nullable: true),
                    EmailSubject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EmailBody = table.Column<string>(type: "text", nullable: false),
                    TranslatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TranslationProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolboxTalkTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolboxTalkTranslations_ToolboxTalks_ToolboxTalkId",
                        column: x => x.ToolboxTalkId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ToolboxTalkVideoTranslations",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolboxTalkId = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    OriginalVideoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TranslatedVideoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    ExternalProjectId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolboxTalkVideoTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolboxTalkVideoTranslations_ToolboxTalks_ToolboxTalkId",
                        column: x => x.ToolboxTalkId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledTalks",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolboxTalkId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequiredDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    RemindersSent = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastReminderAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LanguageCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "en"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledTalks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledTalks_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledTalks_ToolboxTalkSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalkSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ScheduledTalks_ToolboxTalks_ToolboxTalkId",
                        column: x => x.ToolboxTalkId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ToolboxTalkScheduleAssignments",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolboxTalkScheduleAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolboxTalkScheduleAssignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ToolboxTalkScheduleAssignments_ToolboxTalkSchedules_Schedul~",
                        column: x => x.ScheduleId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalkSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledTalkCompletions",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledTalkId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalTimeSpentSeconds = table.Column<int>(type: "integer", nullable: false),
                    VideoWatchPercent = table.Column<int>(type: "integer", nullable: true),
                    QuizScore = table.Column<int>(type: "integer", nullable: true),
                    QuizMaxScore = table.Column<int>(type: "integer", nullable: true),
                    QuizPassed = table.Column<bool>(type: "boolean", nullable: true),
                    SignatureData = table.Column<string>(type: "text", nullable: false),
                    SignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SignedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IPAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CertificateUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledTalkCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledTalkCompletions_ScheduledTalks_ScheduledTalkId",
                        column: x => x.ScheduledTalkId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ScheduledTalks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledTalkQuizAttempts",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledTalkId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Answers = table.Column<string>(type: "text", nullable: false, defaultValue: "{}"),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    MaxScore = table.Column<int>(type: "integer", nullable: false),
                    Percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    AttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledTalkQuizAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledTalkQuizAttempts_ScheduledTalks_ScheduledTalkId",
                        column: x => x.ScheduledTalkId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ScheduledTalks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledTalkSectionProgress",
                schema: "toolbox_talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledTalkId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TimeSpentSeconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledTalkSectionProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledTalkSectionProgress_ScheduledTalks_ScheduledTalkId",
                        column: x => x.ScheduledTalkId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ScheduledTalks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduledTalkSectionProgress_ToolboxTalkSections_SectionId",
                        column: x => x.SectionId,
                        principalSchema: "toolbox_talks",
                        principalTable: "ToolboxTalkSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_talk_completions_completed_at",
                schema: "toolbox_talks",
                table: "ScheduledTalkCompletions",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_talk_completions_talk",
                schema: "toolbox_talks",
                table: "ScheduledTalkCompletions",
                column: "ScheduledTalkId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_talk_quiz_attempts_talk",
                schema: "toolbox_talks",
                table: "ScheduledTalkQuizAttempts",
                column: "ScheduledTalkId");

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_talk_quiz_attempts_talk_attempt",
                schema: "toolbox_talks",
                table: "ScheduledTalkQuizAttempts",
                columns: new[] { "ScheduledTalkId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_talks_due_date",
                schema: "toolbox_talks",
                table: "ScheduledTalks",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_talks_employee",
                schema: "toolbox_talks",
                table: "ScheduledTalks",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_talks_schedule",
                schema: "toolbox_talks",
                table: "ScheduledTalks",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_talks_status",
                schema: "toolbox_talks",
                table: "ScheduledTalks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_talks_talk",
                schema: "toolbox_talks",
                table: "ScheduledTalks",
                column: "ToolboxTalkId");

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_talks_tenant",
                schema: "toolbox_talks",
                table: "ScheduledTalks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_talks_tenant_employee_status",
                schema: "toolbox_talks",
                table: "ScheduledTalks",
                columns: new[] { "TenantId", "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_talk_section_progress_section",
                schema: "toolbox_talks",
                table: "ScheduledTalkSectionProgress",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_talk_section_progress_talk",
                schema: "toolbox_talks",
                table: "ScheduledTalkSectionProgress",
                column: "ScheduledTalkId");

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_talk_section_progress_talk_section",
                schema: "toolbox_talks",
                table: "ScheduledTalkSectionProgress",
                columns: new[] { "ScheduledTalkId", "SectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_questions_talk",
                schema: "toolbox_talks",
                table: "ToolboxTalkQuestions",
                column: "ToolboxTalkId");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_questions_talk_number",
                schema: "toolbox_talks",
                table: "ToolboxTalkQuestions",
                columns: new[] { "ToolboxTalkId", "QuestionNumber" });

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talks_tenant",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talks_tenant_deleted_active",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                columns: new[] { "TenantId", "IsDeleted", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talks_title",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_schedule_assignments_employee",
                schema: "toolbox_talks",
                table: "ToolboxTalkScheduleAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_schedule_assignments_schedule",
                schema: "toolbox_talks",
                table: "ToolboxTalkScheduleAssignments",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_schedule_assignments_schedule_employee",
                schema: "toolbox_talks",
                table: "ToolboxTalkScheduleAssignments",
                columns: new[] { "ScheduleId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_schedules_next_run",
                schema: "toolbox_talks",
                table: "ToolboxTalkSchedules",
                column: "NextRunDate");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_schedules_status",
                schema: "toolbox_talks",
                table: "ToolboxTalkSchedules",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_schedules_talk",
                schema: "toolbox_talks",
                table: "ToolboxTalkSchedules",
                column: "ToolboxTalkId");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_schedules_tenant",
                schema: "toolbox_talks",
                table: "ToolboxTalkSchedules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_schedules_tenant_status_date",
                schema: "toolbox_talks",
                table: "ToolboxTalkSchedules",
                columns: new[] { "TenantId", "Status", "ScheduledDate" });

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_sections_talk",
                schema: "toolbox_talks",
                table: "ToolboxTalkSections",
                column: "ToolboxTalkId");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_sections_talk_number",
                schema: "toolbox_talks",
                table: "ToolboxTalkSections",
                columns: new[] { "ToolboxTalkId", "SectionNumber" });

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_settings_tenant",
                schema: "toolbox_talks",
                table: "ToolboxTalkSettings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_translations_talk",
                schema: "toolbox_talks",
                table: "ToolboxTalkTranslations",
                column: "ToolboxTalkId");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_translations_talk_language",
                schema: "toolbox_talks",
                table: "ToolboxTalkTranslations",
                columns: new[] { "ToolboxTalkId", "LanguageCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_translations_tenant",
                schema: "toolbox_talks",
                table: "ToolboxTalkTranslations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_video_translations_status",
                schema: "toolbox_talks",
                table: "ToolboxTalkVideoTranslations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_video_translations_talk",
                schema: "toolbox_talks",
                table: "ToolboxTalkVideoTranslations",
                column: "ToolboxTalkId");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_video_translations_talk_language",
                schema: "toolbox_talks",
                table: "ToolboxTalkVideoTranslations",
                columns: new[] { "ToolboxTalkId", "LanguageCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_talk_video_translations_tenant",
                schema: "toolbox_talks",
                table: "ToolboxTalkVideoTranslations",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledTalkCompletions",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "ScheduledTalkQuizAttempts",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "ScheduledTalkSectionProgress",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "ToolboxTalkQuestions",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "ToolboxTalkScheduleAssignments",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "ToolboxTalkSettings",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "ToolboxTalkTranslations",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "ToolboxTalkVideoTranslations",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "ScheduledTalks",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "ToolboxTalkSections",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "ToolboxTalkSchedules",
                schema: "toolbox_talks");

            migrationBuilder.DropTable(
                name: "ToolboxTalks",
                schema: "toolbox_talks");

            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                table: "Employees");
        }
    }
}
