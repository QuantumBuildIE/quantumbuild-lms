using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRamsNotificationLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "rams");

            migrationBuilder.CreateTable(
                name: "RamsControlMeasureLibrary",
                schema: "rams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Hierarchy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ApplicableToCategory = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Keywords = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TypicalLikelihoodReduction = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    TypicalSeverityReduction = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RamsControlMeasureLibrary", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RamsDocuments",
                schema: "rams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProjectReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProjectType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ClientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SiteAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AreaOfActivity = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProposedStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ProposedEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SafetyOfficerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Draft"),
                    DateApproved = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovalComments = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MethodStatementBody = table.Column<string>(type: "text", nullable: true),
                    GeneratedPdfUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProposalId = table.Column<Guid>(type: "uuid", nullable: true),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RamsDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RamsHazardLibrary",
                schema: "rams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Keywords = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DefaultLikelihood = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    DefaultSeverity = table.Column<int>(type: "integer", nullable: false, defaultValue: 4),
                    TypicalWhoAtRisk = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RamsHazardLibrary", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RamsLegislationReferences",
                schema: "rams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ShortName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Jurisdiction = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Keywords = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DocumentUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ApplicableCategories = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RamsLegislationReferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RamsSopReferences",
                schema: "rams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SopId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Topic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TaskKeywords = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PolicySnippet = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ProcedureDetails = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ApplicableLegislation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DocumentUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RamsSopReferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RamsNotificationLogs",
                schema: "rams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RamsDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    NotificationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BodyPreview = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WasSent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TriggeredByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TriggeredByUserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RamsNotificationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RamsNotificationLogs_RamsDocuments_RamsDocumentId",
                        column: x => x.RamsDocumentId,
                        principalSchema: "rams",
                        principalTable: "RamsDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RamsRiskAssessments",
                schema: "rams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RamsDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskActivity = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LocationArea = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HazardIdentified = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    WhoAtRisk = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InitialLikelihood = table.Column<int>(type: "integer", nullable: false),
                    InitialSeverity = table.Column<int>(type: "integer", nullable: false),
                    ControlMeasures = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RelevantLegislation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReferenceSops = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ResidualLikelihood = table.Column<int>(type: "integer", nullable: false),
                    ResidualSeverity = table.Column<int>(type: "integer", nullable: false),
                    IsAiGenerated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    AiGeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RamsRiskAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RamsRiskAssessments_RamsDocuments_RamsDocumentId",
                        column: x => x.RamsDocumentId,
                        principalSchema: "rams",
                        principalTable: "RamsDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RamsHazardControlLinks",
                schema: "rams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HazardLibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ControlMeasureLibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RamsHazardControlLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RamsHazardControlLinks_RamsControlMeasureLibrary_ControlMea~",
                        column: x => x.ControlMeasureLibraryId,
                        principalSchema: "rams",
                        principalTable: "RamsControlMeasureLibrary",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RamsHazardControlLinks_RamsHazardLibrary_HazardLibraryId",
                        column: x => x.HazardLibraryId,
                        principalSchema: "rams",
                        principalTable: "RamsHazardLibrary",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RamsMcpAuditLogs",
                schema: "rams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RamsDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    RiskAssessmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InputPrompt = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    InputContext = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    AiResponse = table.Column<string>(type: "text", nullable: true),
                    ExtractedContent = table.Column<string>(type: "text", nullable: true),
                    ModelUsed = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true),
                    CostEstimate = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: true),
                    WasAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResponseTimeMs = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RamsMcpAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RamsMcpAuditLogs_RamsDocuments_RamsDocumentId",
                        column: x => x.RamsDocumentId,
                        principalSchema: "rams",
                        principalTable: "RamsDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RamsMcpAuditLogs_RamsRiskAssessments_RiskAssessmentId",
                        column: x => x.RiskAssessmentId,
                        principalSchema: "rams",
                        principalTable: "RamsRiskAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RamsMethodSteps",
                schema: "rams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RamsDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepNumber = table.Column<int>(type: "integer", nullable: false),
                    StepTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DetailedProcedure = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    LinkedRiskAssessmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequiredPermits = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequiresSignoff = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SignoffUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RamsMethodSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RamsMethodSteps_RamsDocuments_RamsDocumentId",
                        column: x => x.RamsDocumentId,
                        principalSchema: "rams",
                        principalTable: "RamsDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RamsMethodSteps_RamsRiskAssessments_LinkedRiskAssessmentId",
                        column: x => x.LinkedRiskAssessmentId,
                        principalSchema: "rams",
                        principalTable: "RamsRiskAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rams_control_measure_library_code",
                schema: "rams",
                table: "RamsControlMeasureLibrary",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "ix_rams_control_measure_library_hierarchy",
                schema: "rams",
                table: "RamsControlMeasureLibrary",
                column: "Hierarchy");

            migrationBuilder.CreateIndex(
                name: "ix_rams_control_measure_library_tenant",
                schema: "rams",
                table: "RamsControlMeasureLibrary",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_rams_control_measure_library_tenant_code_unique",
                schema: "rams",
                table: "RamsControlMeasureLibrary",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rams_control_measure_library_tenant_deleted_active",
                schema: "rams",
                table: "RamsControlMeasureLibrary",
                columns: new[] { "TenantId", "IsDeleted", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "ix_rams_documents_reference",
                schema: "rams",
                table: "RamsDocuments",
                column: "ProjectReference");

            migrationBuilder.CreateIndex(
                name: "ix_rams_documents_status",
                schema: "rams",
                table: "RamsDocuments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_rams_documents_tenant",
                schema: "rams",
                table: "RamsDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_rams_documents_tenant_deleted_status",
                schema: "rams",
                table: "RamsDocuments",
                columns: new[] { "TenantId", "IsDeleted", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_rams_documents_tenant_reference_unique",
                schema: "rams",
                table: "RamsDocuments",
                columns: new[] { "TenantId", "ProjectReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rams_hazard_control_links_control",
                schema: "rams",
                table: "RamsHazardControlLinks",
                column: "ControlMeasureLibraryId");

            migrationBuilder.CreateIndex(
                name: "ix_rams_hazard_control_links_hazard",
                schema: "rams",
                table: "RamsHazardControlLinks",
                column: "HazardLibraryId");

            migrationBuilder.CreateIndex(
                name: "ix_rams_hazard_control_links_hazard_control_unique",
                schema: "rams",
                table: "RamsHazardControlLinks",
                columns: new[] { "HazardLibraryId", "ControlMeasureLibraryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rams_hazard_library_category",
                schema: "rams",
                table: "RamsHazardLibrary",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "ix_rams_hazard_library_code",
                schema: "rams",
                table: "RamsHazardLibrary",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "ix_rams_hazard_library_tenant",
                schema: "rams",
                table: "RamsHazardLibrary",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_rams_hazard_library_tenant_code_unique",
                schema: "rams",
                table: "RamsHazardLibrary",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rams_hazard_library_tenant_deleted_active",
                schema: "rams",
                table: "RamsHazardLibrary",
                columns: new[] { "TenantId", "IsDeleted", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "ix_rams_legislation_references_code",
                schema: "rams",
                table: "RamsLegislationReferences",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "ix_rams_legislation_references_jurisdiction",
                schema: "rams",
                table: "RamsLegislationReferences",
                column: "Jurisdiction");

            migrationBuilder.CreateIndex(
                name: "ix_rams_legislation_references_tenant",
                schema: "rams",
                table: "RamsLegislationReferences",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_rams_legislation_references_tenant_code_unique",
                schema: "rams",
                table: "RamsLegislationReferences",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rams_legislation_references_tenant_deleted_active",
                schema: "rams",
                table: "RamsLegislationReferences",
                columns: new[] { "TenantId", "IsDeleted", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "ix_rams_mcp_audit_logs_document",
                schema: "rams",
                table: "RamsMcpAuditLogs",
                column: "RamsDocumentId");

            migrationBuilder.CreateIndex(
                name: "ix_rams_mcp_audit_logs_requested_at",
                schema: "rams",
                table: "RamsMcpAuditLogs",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "ix_rams_mcp_audit_logs_tenant",
                schema: "rams",
                table: "RamsMcpAuditLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_rams_mcp_audit_logs_tenant_deleted",
                schema: "rams",
                table: "RamsMcpAuditLogs",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_RamsMcpAuditLogs_RiskAssessmentId",
                schema: "rams",
                table: "RamsMcpAuditLogs",
                column: "RiskAssessmentId");

            migrationBuilder.CreateIndex(
                name: "ix_rams_method_steps_document",
                schema: "rams",
                table: "RamsMethodSteps",
                column: "RamsDocumentId");

            migrationBuilder.CreateIndex(
                name: "ix_rams_method_steps_document_order",
                schema: "rams",
                table: "RamsMethodSteps",
                columns: new[] { "RamsDocumentId", "StepNumber" });

            migrationBuilder.CreateIndex(
                name: "ix_rams_method_steps_tenant",
                schema: "rams",
                table: "RamsMethodSteps",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RamsMethodSteps_LinkedRiskAssessmentId",
                schema: "rams",
                table: "RamsMethodSteps",
                column: "LinkedRiskAssessmentId");

            migrationBuilder.CreateIndex(
                name: "ix_rams_notification_logs_attempted_at",
                schema: "rams",
                table: "RamsNotificationLogs",
                column: "AttemptedAt");

            migrationBuilder.CreateIndex(
                name: "ix_rams_notification_logs_document",
                schema: "rams",
                table: "RamsNotificationLogs",
                column: "RamsDocumentId");

            migrationBuilder.CreateIndex(
                name: "ix_rams_notification_logs_status",
                schema: "rams",
                table: "RamsNotificationLogs",
                columns: new[] { "WasSent", "RetryCount" });

            migrationBuilder.CreateIndex(
                name: "ix_rams_notification_logs_tenant",
                schema: "rams",
                table: "RamsNotificationLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_rams_notification_logs_tenant_deleted_date",
                schema: "rams",
                table: "RamsNotificationLogs",
                columns: new[] { "TenantId", "IsDeleted", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_rams_notification_logs_type",
                schema: "rams",
                table: "RamsNotificationLogs",
                column: "NotificationType");

            migrationBuilder.CreateIndex(
                name: "ix_rams_risk_assessments_document",
                schema: "rams",
                table: "RamsRiskAssessments",
                column: "RamsDocumentId");

            migrationBuilder.CreateIndex(
                name: "ix_rams_risk_assessments_document_order",
                schema: "rams",
                table: "RamsRiskAssessments",
                columns: new[] { "RamsDocumentId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "ix_rams_risk_assessments_tenant",
                schema: "rams",
                table: "RamsRiskAssessments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_rams_sop_references_sop_id",
                schema: "rams",
                table: "RamsSopReferences",
                column: "SopId");

            migrationBuilder.CreateIndex(
                name: "ix_rams_sop_references_tenant",
                schema: "rams",
                table: "RamsSopReferences",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_rams_sop_references_tenant_deleted_active",
                schema: "rams",
                table: "RamsSopReferences",
                columns: new[] { "TenantId", "IsDeleted", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "ix_rams_sop_references_tenant_sop_id_unique",
                schema: "rams",
                table: "RamsSopReferences",
                columns: new[] { "TenantId", "SopId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RamsHazardControlLinks",
                schema: "rams");

            migrationBuilder.DropTable(
                name: "RamsLegislationReferences",
                schema: "rams");

            migrationBuilder.DropTable(
                name: "RamsMcpAuditLogs",
                schema: "rams");

            migrationBuilder.DropTable(
                name: "RamsMethodSteps",
                schema: "rams");

            migrationBuilder.DropTable(
                name: "RamsNotificationLogs",
                schema: "rams");

            migrationBuilder.DropTable(
                name: "RamsSopReferences",
                schema: "rams");

            migrationBuilder.DropTable(
                name: "RamsControlMeasureLibrary",
                schema: "rams");

            migrationBuilder.DropTable(
                name: "RamsHazardLibrary",
                schema: "rams");

            migrationBuilder.DropTable(
                name: "RamsRiskAssessments",
                schema: "rams");

            migrationBuilder.DropTable(
                name: "RamsDocuments",
                schema: "rams");
        }
    }
}
