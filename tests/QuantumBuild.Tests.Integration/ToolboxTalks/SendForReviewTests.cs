using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Core.Infrastructure.Identity;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.SendForReview;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for GET /api/toolbox-talks/{id}/send-for-review/preview and
/// POST /api/toolbox-talks/{id}/send-for-review (Low-Score External-Review Chunk 3).
///
/// Each test creates its own tenant (Respawner does not reset the toolbox_talks schema, so
/// sharing a tenant across tests risks colliding on the TenantReviewerConfiguration
/// {TenantId, LanguageCode} uniqueness constraints — see TenantReviewerConfigurationTests).
/// </summary>
[Collection("Integration")]
public class SendForReviewTests : IntegrationTestBase
{
    public SendForReviewTests(CustomWebApplicationFactory factory) : base(factory) { }

    // The API serialises enums as strings (Program.cs registers JsonStringEnumConverter globally),
    // but the shared test HttpClientExtensions.GetFromJsonAsync helper does not register a matching
    // converter — so DTOs with enum properties (ReviewerResolutionSource here) must be read with
    // these explicit options rather than the shared helper.
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(ResponseJsonOptions);

    private async Task<PreviewSendForReviewDto?> GetPreviewAsync(HttpClient client, Guid talkId)
    {
        var response = await client.GetAsync(PreviewUrl(talkId));
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<PreviewSendForReviewDto>(response);
    }

    // ── Tenant / client helpers ──────────────────────────────────────────────

    private async Task<Guid> CreateTenantAsync()
    {
        var tenantId = Guid.NewGuid();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<Tenant>().Add(new Tenant
        {
            Id = tenantId,
            Name = $"SendForReview Test Tenant {tenantId:N}",
            Code = tenantId.ToString("N")[..8],
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test-seeder",
        });
        await db.SaveChangesAsync();
        return tenantId;
    }

    private HttpClient CreateAdminClient(Guid tenantId) =>
        Factory.CreateAuthenticatedClient(Guid.NewGuid(), $"admin.{Guid.NewGuid():N}@example.com", tenantId, new[] { "Admin" }, Permissions.GetAll());

    private HttpClient CreateViewOnlyClient(Guid tenantId) =>
        Factory.CreateAuthenticatedClient(Guid.NewGuid(), $"viewer.{Guid.NewGuid():N}@example.com", tenantId, new[] { "Operator" }, new[] { Permissions.Learnings.View });

    // ── Data seeding helpers ─────────────────────────────────────────────────

    private async Task<Guid> CreateTalkAsync(Guid tenantId)
    {
        var talkId = Guid.NewGuid();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<ToolboxTalk>().Add(new ToolboxTalk
        {
            Id = talkId,
            TenantId = tenantId,
            Code = talkId.ToString("N")[..8],
            Title = "Send For Review Test Talk",
            Description = "Talk for send-for-review controller tests",
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

    /// <summary>Seeds a translation with enough placeholder sections that any FailingSectionIndices we seed are in range.</summary>
    private async Task SeedTranslationAsync(Guid tenantId, Guid talkId, string languageCode, int sectionCount)
    {
        var sections = Enumerable.Range(0, sectionCount)
            .Select(i => new { SectionId = Guid.NewGuid(), Title = $"Section {i}", Content = $"Content {i}" })
            .ToList();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<ToolboxTalkTranslation>().Add(new ToolboxTalkTranslation
        {
            TenantId = tenantId,
            ToolboxTalkId = talkId,
            LanguageCode = languageCode,
            TranslatedTitle = $"Test translation ({languageCode})",
            TranslatedSections = JsonSerializer.Serialize(sections),
            TranslatedAt = DateTime.UtcNow,
            TranslationProvider = "test"
        });
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedValidationRunAsync(Guid tenantId, Guid talkId, string languageCode, DateTime? createdAt = null)
    {
        var runId = Guid.NewGuid();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<TranslationValidationRun>().Add(new TranslationValidationRun
        {
            Id = runId,
            TenantId = tenantId,
            ToolboxTalkId = talkId,
            LanguageCode = languageCode,
            Status = ValidationRunStatus.Completed,
            OverallOutcome = ValidationOutcome.Fail,
            PassThreshold = 75,
            SourceLanguage = "en",
            CompletedAt = DateTime.UtcNow,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();
        return runId;
    }

    private async Task SeedValidationResultAsync(Guid runId, int sectionIndex, ValidationOutcome outcome)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<TranslationValidationResult>().Add(new TranslationValidationResult
        {
            Id = Guid.NewGuid(),
            ValidationRunId = runId,
            SectionIndex = sectionIndex,
            SectionTitle = $"Section {sectionIndex}",
            OriginalText = "Original text",
            TranslatedText = "Translated text",
            Outcome = outcome,
            EngineOutcome = outcome,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedEventAsync(Guid tenantId, Guid talkId, string languageCode, string eventType)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<WorkflowEvent>().Add(new WorkflowEvent
        {
            TenantId = tenantId,
            WorkflowType = WorkflowType.Translation,
            TargetEntityId = talkId,
            TargetEntitySubKey = languageCode,
            EventType = eventType,
            TriggeredByType = TriggeredByType.User,
            OccurredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Makes a language workflow-eligible for InitiateExternalReview (Validated state).</summary>
    private Task MakeEligibleAsync(Guid tenantId, Guid talkId, string languageCode) =>
        SeedEventAsync(tenantId, talkId, languageCode, WorkflowEventTypes.ValidationCompleted);

    private async Task<Guid> CreateReviewerConfigAsync(HttpClient adminClient, string? languageCode, string? email = null, string? name = null)
    {
        var response = await adminClient.PostAsJsonAsync("/api/tenant-reviewer-configurations", new
        {
            LanguageCode = languageCode,
            ReviewerEmail = email ?? $"reviewer.{Guid.NewGuid():N}@example.com",
            ReviewerName = name,
        });
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"CreateReviewerConfigAsync failed: {response.StatusCode} {body}");
        }
        var dto = await response.Content.ReadFromJsonAsync<ReviewerConfigDto>();
        return dto!.Id;
    }

    private async Task<List<ExternalParticipantInvitation>> GetInvitationsAsync(Guid talkId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<ExternalParticipantInvitation>()
            .IgnoreQueryFilters()
            .Where(i => i.TargetEntityId == talkId)
            .ToListAsync();
    }

    private record ReviewerConfigDto(Guid Id, string? LanguageCode, string ReviewerEmail, string? ReviewerName);

    private string PreviewUrl(Guid talkId) => $"/api/toolbox-talks/{talkId}/send-for-review/preview";
    private string SendUrl(Guid talkId) => $"/api/toolbox-talks/{talkId}/send-for-review";

    // ── Preview query tests ──────────────────────────────────────────────────

    [Fact]
    public async Task Preview_NoRuns_ReturnsEmptyNotBlocked()
    {
        var tenantId = await CreateTenantAsync();
        var admin = CreateAdminClient(tenantId);
        var talkId = await CreateTalkAsync(tenantId);

        var dto = await GetPreviewAsync(admin, talkId);

        dto.Should().NotBeNull();
        dto!.Languages.Should().BeEmpty();
        dto.Blocked.Should().BeFalse();
    }

    [Fact]
    public async Task Preview_RunsButNoFailures_ReturnsEmptyNotBlocked()
    {
        var tenantId = await CreateTenantAsync();
        var admin = CreateAdminClient(tenantId);
        var talkId = await CreateTalkAsync(tenantId);
        var runId = await SeedValidationRunAsync(tenantId, talkId, "fr");
        await SeedValidationResultAsync(runId, 0, ValidationOutcome.Pass);

        var dto = await GetPreviewAsync(admin, talkId);

        dto!.Languages.Should().BeEmpty();
        dto.Blocked.Should().BeFalse();
    }

    [Fact]
    public async Task Preview_OneLanguageFailing_LanguageSpecificReviewer_NotBlocked()
    {
        var tenantId = await CreateTenantAsync();
        var admin = CreateAdminClient(tenantId);
        var talkId = await CreateTalkAsync(tenantId);
        await SeedTranslationAsync(tenantId, talkId, "fr", 3);
        await MakeEligibleAsync(tenantId, talkId, "fr");
        var runId = await SeedValidationRunAsync(tenantId, talkId, "fr");
        await SeedValidationResultAsync(runId, 0, ValidationOutcome.Fail);
        await SeedValidationResultAsync(runId, 1, ValidationOutcome.Pass);
        await CreateReviewerConfigAsync(admin, "fr", "fr-reviewer@example.com", "French Reviewer");

        var dto = await GetPreviewAsync(admin, talkId);

        dto!.Blocked.Should().BeFalse();
        dto.Languages.Should().HaveCount(1);
        var lang = dto.Languages.Single();
        lang.LanguageCode.Should().Be("fr");
        lang.FailingSectionIndices.Should().Equal(0);
        lang.FailingSectionCount.Should().Be(1);
        lang.ResolvedReviewerEmail.Should().Be("fr-reviewer@example.com");
        lang.ResolvedReviewerName.Should().Be("French Reviewer");
        lang.ResolutionSource.Should().Be(ReviewerResolutionSource.LanguageSpecific);
        lang.WorkflowStateEligible.Should().BeTrue();
    }

    [Fact]
    public async Task Preview_OnlyFallbackReviewerConfigured_NotBlocked_SourceIsFallback()
    {
        var tenantId = await CreateTenantAsync();
        var admin = CreateAdminClient(tenantId);
        var talkId = await CreateTalkAsync(tenantId);
        await SeedTranslationAsync(tenantId, talkId, "de", 2);
        await MakeEligibleAsync(tenantId, talkId, "de");
        var runId = await SeedValidationRunAsync(tenantId, talkId, "de");
        await SeedValidationResultAsync(runId, 0, ValidationOutcome.Fail);
        await CreateReviewerConfigAsync(admin, null, "fallback@example.com", "Fallback Reviewer");

        var dto = await GetPreviewAsync(admin, talkId);

        dto!.Blocked.Should().BeFalse();
        var lang = dto.Languages.Single();
        lang.ResolvedReviewerEmail.Should().Be("fallback@example.com");
        lang.ResolutionSource.Should().Be(ReviewerResolutionSource.Fallback);
    }

    [Fact]
    public async Task Preview_NoReviewerConfigured_Blocked_SourceIsNone()
    {
        var tenantId = await CreateTenantAsync();
        var admin = CreateAdminClient(tenantId);
        var talkId = await CreateTalkAsync(tenantId);
        await SeedTranslationAsync(tenantId, talkId, "es", 2);
        await MakeEligibleAsync(tenantId, talkId, "es");
        var runId = await SeedValidationRunAsync(tenantId, talkId, "es");
        await SeedValidationResultAsync(runId, 0, ValidationOutcome.Fail);

        var dto = await GetPreviewAsync(admin, talkId);

        dto!.Blocked.Should().BeTrue();
        var lang = dto.Languages.Single();
        lang.ResolvedReviewerEmail.Should().BeNull();
        lang.ResolutionSource.Should().Be(ReviewerResolutionSource.None);
    }

    [Fact]
    public async Task Preview_TwoLanguagesFailing_OneSpecificOneFallback_NotBlocked()
    {
        var tenantId = await CreateTenantAsync();
        var admin = CreateAdminClient(tenantId);
        var talkId = await CreateTalkAsync(tenantId);

        await SeedTranslationAsync(tenantId, talkId, "pl", 2);
        await MakeEligibleAsync(tenantId, talkId, "pl");
        var plRun = await SeedValidationRunAsync(tenantId, talkId, "pl");
        await SeedValidationResultAsync(plRun, 0, ValidationOutcome.Fail);

        await SeedTranslationAsync(tenantId, talkId, "ro", 2);
        await MakeEligibleAsync(tenantId, talkId, "ro");
        var roRun = await SeedValidationRunAsync(tenantId, talkId, "ro");
        await SeedValidationResultAsync(roRun, 0, ValidationOutcome.Fail);

        await CreateReviewerConfigAsync(admin, "pl", "pl-reviewer@example.com");
        await CreateReviewerConfigAsync(admin, null, "fallback@example.com");

        var dto = await GetPreviewAsync(admin, talkId);

        dto!.Blocked.Should().BeFalse();
        dto.Languages.Should().HaveCount(2);
        dto.Languages.Single(l => l.LanguageCode == "pl").ResolutionSource.Should().Be(ReviewerResolutionSource.LanguageSpecific);
        dto.Languages.Single(l => l.LanguageCode == "ro").ResolutionSource.Should().Be(ReviewerResolutionSource.Fallback);
    }

    [Fact]
    public async Task Preview_TwoLanguagesFailing_OneHasReviewerOneDoesNot_Blocked()
    {
        var tenantId = await CreateTenantAsync();
        var admin = CreateAdminClient(tenantId);
        var talkId = await CreateTalkAsync(tenantId);

        await SeedTranslationAsync(tenantId, talkId, "it", 2);
        await MakeEligibleAsync(tenantId, talkId, "it");
        var itRun = await SeedValidationRunAsync(tenantId, talkId, "it");
        await SeedValidationResultAsync(itRun, 0, ValidationOutcome.Fail);

        await SeedTranslationAsync(tenantId, talkId, "nl", 2);
        await MakeEligibleAsync(tenantId, talkId, "nl");
        var nlRun = await SeedValidationRunAsync(tenantId, talkId, "nl");
        await SeedValidationResultAsync(nlRun, 0, ValidationOutcome.Fail);

        await CreateReviewerConfigAsync(admin, "it", "it-reviewer@example.com");

        var dto = await GetPreviewAsync(admin, talkId);

        dto!.Blocked.Should().BeTrue();
        dto.Languages.Single(l => l.LanguageCode == "it").ResolvedReviewerEmail.Should().NotBeNull();
        dto.Languages.Single(l => l.LanguageCode == "nl").ResolvedReviewerEmail.Should().BeNull();
    }

    [Fact]
    public async Task Preview_LanguageFailing_IneligibleWorkflowState_ListedAndBlocked()
    {
        var tenantId = await CreateTenantAsync();
        var admin = CreateAdminClient(tenantId);
        var talkId = await CreateTalkAsync(tenantId);
        await SeedTranslationAsync(tenantId, talkId, "sv", 2);
        // Only TranslationCompleted -> state AIGenerated, not eligible (Validated/ReviewerAccepted/ThirdPartyReviewed required)
        await SeedEventAsync(tenantId, talkId, "sv", WorkflowEventTypes.TranslationCompleted);
        var runId = await SeedValidationRunAsync(tenantId, talkId, "sv");
        await SeedValidationResultAsync(runId, 0, ValidationOutcome.Fail);
        await CreateReviewerConfigAsync(admin, "sv", "sv-reviewer@example.com");

        var dto = await GetPreviewAsync(admin, talkId);

        dto!.Blocked.Should().BeTrue();
        var lang = dto.Languages.Single();
        lang.WorkflowStateEligible.Should().BeFalse();
        lang.ResolvedReviewerEmail.Should().NotBeNull(); // reviewer resolved fine; state is what blocks
    }

    [Fact]
    public async Task Preview_OnlyLatestRunPerLanguageCounted()
    {
        var tenantId = await CreateTenantAsync();
        var admin = CreateAdminClient(tenantId);
        var talkId = await CreateTalkAsync(tenantId);
        await SeedTranslationAsync(tenantId, talkId, "da", 5);
        await MakeEligibleAsync(tenantId, talkId, "da");

        var oldRun = await SeedValidationRunAsync(tenantId, talkId, "da", DateTime.UtcNow.AddDays(-2));
        await SeedValidationResultAsync(oldRun, 0, ValidationOutcome.Fail);
        await SeedValidationResultAsync(oldRun, 1, ValidationOutcome.Fail);
        await SeedValidationResultAsync(oldRun, 2, ValidationOutcome.Fail);

        var newRun = await SeedValidationRunAsync(tenantId, talkId, "da", DateTime.UtcNow);
        await SeedValidationResultAsync(newRun, 4, ValidationOutcome.Fail);

        await CreateReviewerConfigAsync(admin, "da", "da-reviewer@example.com");

        var dto = await GetPreviewAsync(admin, talkId);

        var lang = dto!.Languages.Single();
        lang.FailingSectionIndices.Should().Equal(4);
    }

    // ── Send command tests ───────────────────────────────────────────────────

    [Fact]
    public async Task Send_Blocked_ReviewerMissing_FailsAndCreatesNoInvitations()
    {
        var tenantId = await CreateTenantAsync();
        var admin = CreateAdminClient(tenantId);
        var talkId = await CreateTalkAsync(tenantId);
        await SeedTranslationAsync(tenantId, talkId, "fi", 2);
        await MakeEligibleAsync(tenantId, talkId, "fi");
        var runId = await SeedValidationRunAsync(tenantId, talkId, "fi");
        await SeedValidationResultAsync(runId, 0, ValidationOutcome.Fail);
        // No reviewer configured for "fi" and no fallback.

        var response = await admin.PostAsync(SendUrl(talkId), null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await ReadJsonAsync<SendForReviewResultDto>(response);
        body!.Blocked.Should().BeTrue();
        body.BlockedLanguages.Should().ContainSingle(l => l.LanguageCode == "fi" && l.ReviewerMissing);

        var invitations = await GetInvitationsAsync(talkId);
        invitations.Should().BeEmpty();
    }

    [Fact]
    public async Task Send_NotBlocked_TwoLanguages_CreatesOneInvitationPerLanguageWithCorrectSections()
    {
        var tenantId = await CreateTenantAsync();
        var admin = CreateAdminClient(tenantId);
        var talkId = await CreateTalkAsync(tenantId);

        await SeedTranslationAsync(tenantId, talkId, "cs", 3);
        await MakeEligibleAsync(tenantId, talkId, "cs");
        var csRun = await SeedValidationRunAsync(tenantId, talkId, "cs");
        await SeedValidationResultAsync(csRun, 0, ValidationOutcome.Fail);
        await SeedValidationResultAsync(csRun, 2, ValidationOutcome.Fail);
        await CreateReviewerConfigAsync(admin, "cs", "cs-reviewer@example.com");

        await SeedTranslationAsync(tenantId, talkId, "hu", 2);
        await MakeEligibleAsync(tenantId, talkId, "hu");
        var huRun = await SeedValidationRunAsync(tenantId, talkId, "hu");
        await SeedValidationResultAsync(huRun, 1, ValidationOutcome.Fail);
        await CreateReviewerConfigAsync(admin, null, "fallback@example.com");

        var response = await admin.PostAsync(SendUrl(talkId), null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync<SendForReviewResultDto>(response);
        body!.Success.Should().BeTrue();
        body.Blocked.Should().BeFalse();
        body.LanguageResults.Should().HaveCount(2);
        body.LanguageResults.Should().OnlyContain(r => r.Success && r.InvitationId != null);

        var invitations = await GetInvitationsAsync(talkId);
        invitations.Should().HaveCount(2);

        var csInvitation = invitations.Single(i => i.TargetEntitySubKey == "cs");
        csInvitation.InvitedEmail.Should().Be("cs-reviewer@example.com");
        csInvitation.EditableSectionIndices.Should().Equal(0, 2);

        var huInvitation = invitations.Single(i => i.TargetEntitySubKey == "hu");
        huInvitation.InvitedEmail.Should().Be("fallback@example.com");
        huInvitation.EditableSectionIndices.Should().Equal(1);
    }

    [Fact]
    public async Task Send_NoFailingSections_ReturnsBadRequest()
    {
        var tenantId = await CreateTenantAsync();
        var admin = CreateAdminClient(tenantId);
        var talkId = await CreateTalkAsync(tenantId);
        var runId = await SeedValidationRunAsync(tenantId, talkId, "no");
        await SeedValidationResultAsync(runId, 0, ValidationOutcome.Pass);

        var response = await admin.PostAsync(SendUrl(talkId), null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// InitiateExternalReview itself has no idempotency guard (see research: no existing-invitation
    /// check before creating a new one) — but a successful call fires WorkflowEventTypes.ExternalReviewInitiated,
    /// which transitions the language's state to AwaitingThirdParty. Since PreviewSendForReviewQuery's
    /// WorkflowStateEligible check uses the same three-state eligibility as InitiateExternalReview, a
    /// second Send for the same language is naturally blocked (409) by that recomputed eligibility
    /// check — not by any dedupe logic added in this chunk. Documents the observed behaviour; do not
    /// add explicit dedupe here, it is out of scope (chunk 3 does not modify InitiateExternalReview).
    /// </summary>
    [Fact]
    public async Task Send_CalledTwice_SecondCallBlockedByWorkflowStateTransition()
    {
        var tenantId = await CreateTenantAsync();
        var admin = CreateAdminClient(tenantId);
        var talkId = await CreateTalkAsync(tenantId);
        await SeedTranslationAsync(tenantId, talkId, "pt", 2);
        await MakeEligibleAsync(tenantId, talkId, "pt");
        var runId = await SeedValidationRunAsync(tenantId, talkId, "pt");
        await SeedValidationResultAsync(runId, 0, ValidationOutcome.Fail);
        await CreateReviewerConfigAsync(admin, "pt", "pt-reviewer@example.com");

        var first = await admin.PostAsync(SendUrl(talkId), null);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await admin.PostAsync(SendUrl(talkId), null);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var secondBody = await ReadJsonAsync<SendForReviewResultDto>(second);
        secondBody!.BlockedLanguages.Should().ContainSingle(l => l.LanguageCode == "pt" && l.WorkflowStateIneligible);

        var invitations = await GetInvitationsAsync(talkId);
        invitations.Should().HaveCount(1);
    }

    // ── Auth tests ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Preview_Unauthenticated_Returns401()
    {
        await AssertUnauthorizedAsync(PreviewUrl(Guid.NewGuid()));
    }

    [Fact]
    public async Task Send_Unauthenticated_Returns401()
    {
        await AssertUnauthorizedAsync(SendUrl(Guid.NewGuid()), HttpMethod.Post);
    }

    [Fact]
    public async Task Send_ViewOnlyPermission_Returns403()
    {
        var tenantId = await CreateTenantAsync();
        var viewer = CreateViewOnlyClient(tenantId);
        var talkId = await CreateTalkAsync(tenantId);

        var response = await viewer.PostAsync(SendUrl(talkId), null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
