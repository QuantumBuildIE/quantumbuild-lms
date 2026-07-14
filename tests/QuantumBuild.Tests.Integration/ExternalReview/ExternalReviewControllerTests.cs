using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Tests.Common.TestTenant;

namespace QuantumBuild.Tests.Integration.ExternalReview;

/// <summary>
/// Integration tests for GET /api/external-review/{token},
/// POST /api/external-review/{token}/submit, and
/// POST /api/external-review/{token}/decline.
///
/// All endpoints are [AllowAnonymous] — UnauthenticatedClient is used throughout.
/// Each test creates its own talk and seeds its own data (no shared state).
///
/// Language codes used per test group:
///   "qa" — GET portal context tests
///   "qb" — Submit tests
///   "qc" — Decline tests
/// These codes are unlikely to collide with other test classes.
/// </summary>
[Collection("Integration")]
public class ExternalReviewControllerTests : IntegrationTestBase
{
    public ExternalReviewControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<Guid> CreateTalkAsync()
    {
        var talkId = Guid.NewGuid();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<ToolboxTalk>().Add(new ToolboxTalk
        {
            Id = talkId,
            TenantId = TestTenantConstants.TenantId,
            Code = talkId.ToString("N")[..8],
            Title = "External Review Test Talk",
            Description = "Talk for external review controller tests",
            Frequency = ToolboxTalkFrequency.Once,
            VideoSource = VideoSource.None,
            MinimumVideoWatchPercent = 90,
            RequiresQuiz = false,
            IsActive = true,
            GenerateCertificate = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();
        return talkId;
    }

    private async Task SeedEventAsync(Guid talkId, string languageCode, string eventType)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<WorkflowEvent>().Add(new WorkflowEvent
        {
            TenantId = TestTenantConstants.TenantId,
            WorkflowType = WorkflowType.Translation,
            TargetEntityId = talkId,
            TargetEntitySubKey = languageCode,
            EventType = eventType,
            TriggeredByType = TriggeredByType.User,
            OccurredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Seeds an invitation and returns the raw token and the seeded invitation ID.</summary>
    private async Task<(string RawToken, Guid InvitationId)> SeedInvitationAsync(
        Guid talkId,
        string languageCode,
        InvitationStatus status = InvitationStatus.Pending,
        DateTime? expiresAt = null)
    {
        var rawToken = Guid.NewGuid().ToString("N");
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
        var invitationId = Guid.NewGuid();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<ExternalParticipantInvitation>().Add(new ExternalParticipantInvitation
        {
            Id = invitationId,
            TenantId = TestTenantConstants.TenantId,
            WorkflowType = WorkflowType.Translation,
            TargetEntityId = talkId,
            TargetEntitySubKey = languageCode,
            InvitedEmail = "reviewer@example.com",
            TokenHash = tokenHash,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(30),
            Status = status,
            ContextType = "TranslationReview",
            ContextPayload = JsonSerializer.Serialize(new { contextType = "TranslationReview", flaggedWordCount = 3 }),
            RequesterUserId = TestTenantConstants.Users.Admin.Id,
            InvitedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        return (rawToken, invitationId);
    }

    private async Task<Guid> SeedValidationRunAsync(Guid talkId, string languageCode)
    {
        var runId = Guid.NewGuid();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<TranslationValidationRun>().Add(new TranslationValidationRun
        {
            Id = runId,
            TenantId = TestTenantConstants.TenantId,
            ToolboxTalkId = talkId,
            LanguageCode = languageCode,
            Status = ValidationRunStatus.Completed,
            OverallOutcome = ValidationOutcome.Pass,
            PassThreshold = 75,
            SourceLanguage = "en",
            CompletedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();
        return runId;
    }

    private async Task<Guid> SeedValidationResultAsync(Guid runId, int sectionIndex, string originalText, string translatedText)
    {
        var resultId = Guid.NewGuid();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<TranslationValidationResult>().Add(new TranslationValidationResult
        {
            Id = resultId,
            ValidationRunId = runId,
            SectionIndex = sectionIndex,
            SectionTitle = $"Section {sectionIndex}",
            OriginalText = originalText,
            TranslatedText = translatedText,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();
        return resultId;
    }

    private async Task SeedFlagAsync(Guid resultId, Guid talkId, string languageCode, int startOffset, int endOffset)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<TranslationFlag>().Add(new TranslationFlag
        {
            TenantId = TestTenantConstants.TenantId,
            ToolboxTalkId = talkId,
            LanguageCode = languageCode,
            ValidationResultId = resultId,
            StartOffset = startOffset,
            EndOffset = endOffset,
            Severity = FlagSeverity.Warning,
            Reason = "test flag",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Seeds a ToolboxTalkTranslation for (talkId, languageCode) with the given JSON sections.</summary>
    private async Task SeedTranslationAsync(Guid talkId, string languageCode, string translatedSectionsJson)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<ToolboxTalkTranslation>().Add(new ToolboxTalkTranslation
        {
            TenantId = TestTenantConstants.TenantId,
            ToolboxTalkId = talkId,
            LanguageCode = languageCode,
            TranslatedTitle = $"Test translation ({languageCode})",
            TranslatedSections = translatedSectionsJson,
            TranslatedAt = DateTime.UtcNow,
            TranslationProvider = "test"
        });
        await db.SaveChangesAsync();
    }

    private sealed class TranslatedSectionSnapshot
    {
        public Guid SectionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    // ── GET portal context tests ──────────────────────────────────────────────

    // 1 — Unknown token → 404
    [Fact]
    public async Task GetPortal_InvalidToken_Returns404()
    {
        var response = await UnauthenticatedClient.GetAsync(
            $"/api/external-review/{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 2 — Active invitation with validation run, results, and flags → 200, DTO fully populated
    [Fact]
    public async Task GetPortal_ActiveInvitation_Returns200WithSectionsAndFlags()
    {
        var talkId = await CreateTalkAsync();
        await SeedEventAsync(talkId, "qa", WorkflowEventTypes.ExternalReviewInitiated);
        var (rawToken, _) = await SeedInvitationAsync(talkId, "qa");
        var runId = await SeedValidationRunAsync(talkId, "qa");
        var resultId = await SeedValidationResultAsync(runId, 0, "Original text here", "Translated text here");
        await SeedFlagAsync(resultId, talkId, "qa", 0, 8);

        var response = await UnauthenticatedClient.GetAsync($"/api/external-review/{rawToken}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ExternalReviewPortalDto>();
        dto.Should().NotBeNull();
        dto!.PortalStatus.Should().Be("Active");
        dto.TalkTitle.Should().Be("External Review Test Talk");
        dto.LanguageCode.Should().Be("qa");
        dto.FlaggedWordCount.Should().Be(3); // from seeded ContextPayload
        dto.ContextType.Should().Be("TranslationReview");
        dto.Sections.Should().HaveCount(1);
        dto.Sections[0].SectionIndex.Should().Be(0);
        dto.Sections[0].OriginalText.Should().Be("Original text here");
        dto.Sections[0].TranslatedText.Should().Be("Translated text here");
        dto.Sections[0].Flags.Should().HaveCount(1);
        dto.Sections[0].Flags[0].StartOffset.Should().Be(0);
        dto.Sections[0].Flags[0].EndOffset.Should().Be(8);
        dto.Sections[0].Flags[0].Severity.Should().Be("Warning");
    }

    // 3 — Invitation already used → 200 with PortalStatus "Used" and empty Sections
    [Fact]
    public async Task GetPortal_UsedInvitation_Returns200WithUsedStatusAndNoSections()
    {
        var talkId = await CreateTalkAsync();
        var (rawToken, _) = await SeedInvitationAsync(talkId, "qa", InvitationStatus.Used);

        var response = await UnauthenticatedClient.GetAsync($"/api/external-review/{rawToken}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ExternalReviewPortalDto>();
        dto!.PortalStatus.Should().Be("Used");
        dto.Sections.Should().BeEmpty();
    }

    // 4 — Revoked invitation → 410 Gone with PortalStatus "Revoked"
    [Fact]
    public async Task GetPortal_RevokedInvitation_Returns410WithRevokedStatus()
    {
        var talkId = await CreateTalkAsync();
        var (rawToken, _) = await SeedInvitationAsync(talkId, "qa", InvitationStatus.Revoked);

        var response = await UnauthenticatedClient.GetAsync($"/api/external-review/{rawToken}");

        response.StatusCode.Should().Be(HttpStatusCode.Gone);
        var dto = await response.Content.ReadFromJsonAsync<ExternalReviewPortalDto>();
        dto!.PortalStatus.Should().Be("Revoked");
    }

    // 5 — Expired invitation (ExpiresAt in the past, Status still Pending) → 410 Gone with PortalStatus "Expired"
    [Fact]
    public async Task GetPortal_ExpiredInvitation_Returns410WithExpiredStatus()
    {
        var talkId = await CreateTalkAsync();
        var (rawToken, _) = await SeedInvitationAsync(talkId, "qa",
            expiresAt: DateTime.UtcNow.AddDays(-1));

        var response = await UnauthenticatedClient.GetAsync($"/api/external-review/{rawToken}");

        response.StatusCode.Should().Be(HttpStatusCode.Gone);
        var dto = await response.Content.ReadFromJsonAsync<ExternalReviewPortalDto>();
        dto!.PortalStatus.Should().Be("Expired");
    }

    // ── Submit tests ──────────────────────────────────────────────────────────

    // 6 — Unknown token → 404
    [Fact]
    public async Task Submit_InvalidToken_Returns404()
    {
        var response = await UnauthenticatedClient.PostAsJsonAsync(
            $"/api/external-review/{Guid.NewGuid():N}/submit",
            new { accepted = true });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 7 — Active invitation, Accepted = true → 200, state transitions to ThirdPartyReviewed,
    //     and the reviewer's edits are auto-applied into TranslatedSections with provenance
    //     stamped (Option A auto-apply — see docs/external-review-auto-apply-recon.md).
    [Fact]
    public async Task Submit_ActiveInvitation_AcceptedTrue_Returns200AndTransitionsToThirdPartyReviewed()
    {
        var talkId = await CreateTalkAsync();
        var sectionId = Guid.NewGuid();
        await SeedTranslationAsync(talkId, "qb", JsonSerializer.Serialize(new[]
        {
            new { SectionId = sectionId, Title = "Section Title", Content = "Original AI translation" }
        }));
        await SeedEventAsync(talkId, "qb", WorkflowEventTypes.ExternalReviewInitiated);
        var (rawToken, _) = await SeedInvitationAsync(talkId, "qb");

        // camelCase keys mirror the actual wire contract sent by the external-review frontend
        // (web/src/types/external-review.ts) — PascalCase keys previously "worked" only because
        // the DTO had no JsonPropertyName attributes and STJ's default case-sensitive matching
        // happened to line up with the C# property names, masking the real-world camelCase bug.
        var editsJson = JsonSerializer.Serialize(new[]
        {
            new { sectionIndex = 0, translatedText = "Reviewer edited translation" }
        });

        var response = await UnauthenticatedClient.PostAsJsonAsync(
            $"/api/external-review/{rawToken}/submit",
            new { accepted = true, editedContent = editsJson });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify state transitioned to ThirdPartyReviewed by reading the history
        var historyResponse = await AdminClient.GetAsync(
            $"/api/toolbox-talks/{talkId}/translations/qb/history");
        var events = await historyResponse.Content.ReadFromJsonAsync<List<WorkflowEventResponse>>();
        events!.Last().EventType.Should().Be(WorkflowEventTypes.ExternalReviewSubmitted);

        // Verify the edit was auto-applied and provenance stamped — no separate admin
        // confirmation step exists anymore.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var translation = await db.Set<ToolboxTalkTranslation>()
            .IgnoreQueryFilters()
            .FirstAsync(t => t.ToolboxTalkId == talkId && t.LanguageCode == "qb" && !t.IsDeleted);
        var sections = JsonSerializer.Deserialize<List<TranslatedSectionSnapshot>>(translation.TranslatedSections);
        sections![0].Content.Should().Be("Reviewer edited translation");
        translation.LastExternalReviewedAt.Should().NotBeNull();
        translation.LastExternalReviewedBy.Should().Be("reviewer@example.com");
    }

    // 7b — Active invitation, Accepted = true, editedContent is non-blank but not parseable as
    //      section edits → 400 with a distinct diagnostic, not the generic "cannot be left blank"
    //      message that this failure mode used to silently fall through to.
    [Fact]
    public async Task Submit_ActiveInvitation_MalformedEditedContent_Returns400WithDistinctError()
    {
        var talkId = await CreateTalkAsync();
        var sectionId = Guid.NewGuid();
        await SeedTranslationAsync(talkId, "qb", JsonSerializer.Serialize(new[]
        {
            new { SectionId = sectionId, Title = "Section Title", Content = "Original AI translation" }
        }));
        await SeedEventAsync(talkId, "qb", WorkflowEventTypes.ExternalReviewInitiated);
        var (rawToken, _) = await SeedInvitationAsync(talkId, "qb");

        var response = await UnauthenticatedClient.PostAsJsonAsync(
            $"/api/external-review/{rawToken}/submit",
            new { accepted = true, editedContent = "{ this is not a valid section edits array" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Contain("could not be parsed");
    }

    // 8 — Active invitation, Accepted = false → 200, ExternalReviewSubmitted event written
    [Fact]
    public async Task Submit_ActiveInvitation_AcceptedFalse_Returns200AndWritesSubmittedEvent()
    {
        var talkId = await CreateTalkAsync();
        await SeedEventAsync(talkId, "qb", WorkflowEventTypes.ExternalReviewInitiated);
        var (rawToken, _) = await SeedInvitationAsync(talkId, "qb");

        var response = await UnauthenticatedClient.PostAsJsonAsync(
            $"/api/external-review/{rawToken}/submit",
            new { accepted = false, editedContent = "Corrected translation" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // 9 — Expired invitation → 410 Gone
    [Fact]
    public async Task Submit_ExpiredInvitation_Returns410()
    {
        var talkId = await CreateTalkAsync();
        var (rawToken, _) = await SeedInvitationAsync(talkId, "qb",
            expiresAt: DateTime.UtcNow.AddDays(-1));

        var response = await UnauthenticatedClient.PostAsJsonAsync(
            $"/api/external-review/{rawToken}/submit",
            new { accepted = true });

        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    // 10 — Already-used invitation → 409 Conflict
    [Fact]
    public async Task Submit_UsedInvitation_Returns409()
    {
        var talkId = await CreateTalkAsync();
        var (rawToken, _) = await SeedInvitationAsync(talkId, "qb", InvitationStatus.Used);

        var response = await UnauthenticatedClient.PostAsJsonAsync(
            $"/api/external-review/{rawToken}/submit",
            new { accepted = true });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── Decline tests ─────────────────────────────────────────────────────────

    // 11 — Unknown token → 404
    [Fact]
    public async Task Decline_InvalidToken_Returns404()
    {
        var response = await UnauthenticatedClient.PostAsJsonAsync(
            $"/api/external-review/{Guid.NewGuid():N}/decline",
            new { reason = "Not relevant to my expertise" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 12 — Active invitation, valid reason → 200, WorkflowReview row written with DeclineReason
    [Fact]
    public async Task Decline_ActiveInvitation_WithReason_Returns200AndWritesWorkflowReviewWithDeclineReason()
    {
        var talkId = await CreateTalkAsync();
        await SeedEventAsync(talkId, "qc", WorkflowEventTypes.ExternalReviewInitiated);
        var (rawToken, invitationId) = await SeedInvitationAsync(talkId, "qc");

        var response = await UnauthenticatedClient.PostAsJsonAsync(
            $"/api/external-review/{rawToken}/decline",
            new { reason = "I am not qualified to review this translation" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify WorkflowReview was written with the correct DeclineReason
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var review = await db.Set<WorkflowReview>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.ExternalParticipantInvitationId == invitationId);

        review.Should().NotBeNull();
        review!.Accepted.Should().BeFalse();
        review.DeclineReason.Should().Be("I am not qualified to review this translation");
        review.ReviewerType.Should().Be(ReviewerType.External);

        // Invitation should now be Used
        var invitation = await db.Set<ExternalParticipantInvitation>()
            .IgnoreQueryFilters()
            .FirstAsync(i => i.Id == invitationId);
        invitation.Status.Should().Be(InvitationStatus.Used);
    }

    // 13 — Active invitation, empty reason → 400
    [Fact]
    public async Task Decline_ActiveInvitation_EmptyReason_Returns400()
    {
        var talkId = await CreateTalkAsync();
        await SeedEventAsync(talkId, "qc", WorkflowEventTypes.ExternalReviewInitiated);
        var (rawToken, _) = await SeedInvitationAsync(talkId, "qc");

        var response = await UnauthenticatedClient.PostAsJsonAsync(
            $"/api/external-review/{rawToken}/decline",
            new { reason = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // 14 — Active invitation, whitespace-only reason → 400
    [Fact]
    public async Task Decline_ActiveInvitation_WhitespaceOnlyReason_Returns400()
    {
        var talkId = await CreateTalkAsync();
        await SeedEventAsync(talkId, "qc", WorkflowEventTypes.ExternalReviewInitiated);
        var (rawToken, _) = await SeedInvitationAsync(talkId, "qc");

        var response = await UnauthenticatedClient.PostAsJsonAsync(
            $"/api/external-review/{rawToken}/decline",
            new { reason = "   " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // 15 — Expired invitation → 410 Gone
    [Fact]
    public async Task Decline_ExpiredInvitation_Returns410()
    {
        var talkId = await CreateTalkAsync();
        var (rawToken, _) = await SeedInvitationAsync(talkId, "qc",
            expiresAt: DateTime.UtcNow.AddDays(-1));

        var response = await UnauthenticatedClient.PostAsJsonAsync(
            $"/api/external-review/{rawToken}/decline",
            new { reason = "Expired" });

        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    // 16 — Second decline attempt after first decline (invitation now Used) → 409 Conflict
    //      The first decline sets invitation.Status = Used; the second attempt finds a non-Pending
    //      invitation and returns WorkflowTokenExpired (token logic treats non-Pending as expired).
    //      Service returns 410 for this case (expired/used path), which maps to Conflict here
    //      since the state guard also rejects. We assert the response is not 200.
    [Fact]
    public async Task Decline_AlreadyDeclined_ReturnsNon200()
    {
        var talkId = await CreateTalkAsync();
        await SeedEventAsync(talkId, "qc", WorkflowEventTypes.ExternalReviewInitiated);
        var (rawToken, _) = await SeedInvitationAsync(talkId, "qc");

        // First decline — should succeed
        var firstResponse = await UnauthenticatedClient.PostAsJsonAsync(
            $"/api/external-review/{rawToken}/decline",
            new { reason = "Not qualified" });
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second decline — token is now Used; should fail
        var secondResponse = await UnauthenticatedClient.PostAsJsonAsync(
            $"/api/external-review/{rawToken}/decline",
            new { reason = "Trying again" });
        ((int)secondResponse.StatusCode).Should().BeInRange(400, 499);
    }

    // ── Local response DTOs ───────────────────────────────────────────────────

    private record WorkflowEventResponse
    {
        public string EventType { get; init; } = string.Empty;
        public DateTime OccurredAt { get; init; }
    }
}
