using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Infrastructure.Identity;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

[Collection("Integration")]
public class TranslationValidationTests : IntegrationTestBase
{
    private readonly string _baseTalkUrl;

    public TranslationValidationTests(CustomWebApplicationFactory factory) : base(factory)
    {
        _baseTalkUrl = $"/api/toolbox-talks/{TestTenantConstants.ToolboxTalks.Talks.BasicTalk}/validation";
    }

    #region Multi-Tenant Isolation

    [Fact]
    public async Task GetRunById_AsTenantB_ReturnNotFound_ForTenantARun()
    {
        // Arrange: Tenant A has a completed run (seeded)
        var runId = TestTenantConstants.TranslationValidation.CompletedRun;
        var tenantBClient = Factory.CreateAuthenticatedClient(
            TestTenantConstants.TenantB.Users.Admin.Id,
            TestTenantConstants.TenantB.Users.Admin.Email,
            TestTenantConstants.TenantB.TenantId,
            new[] { "Admin" },
            Permissions.GetAll());

        // Act: Tenant B admin tries to access Tenant A's run
        var response = await tenantBClient.GetAsync(
            $"{_baseTalkUrl}/runs/{runId}");

        // Assert: Should get 404 (talk not found for tenant B)
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRuns_AsTenantB_ReturnsEmptyList_NotTenantAData()
    {
        // Arrange
        var tenantBClient = Factory.CreateAuthenticatedClient(
            TestTenantConstants.TenantB.Users.Admin.Id,
            TestTenantConstants.TenantB.Users.Admin.Email,
            TestTenantConstants.TenantB.TenantId,
            new[] { "Admin" },
            Permissions.GetAll());

        // Act: Tenant B lists runs for Tenant A's talk
        var response = await tenantBClient.GetAsync(
            $"{_baseTalkUrl}/runs?pageNumber=1&pageSize=10");

        // Assert: Should get 404 since the talk doesn't belong to tenant B
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Glossary CRUD — System Default Protection

    [Fact]
    public async Task AddTermToSystemDefaultSector_Returns400()
    {
        // Arrange: Look up the actual system default glossary (seeded by DataSeeder, ID differs from test constants)
        var context = GetDbContext();
        var systemGlossary = await context.SafetyGlossaries
            .IgnoreQueryFilters()
            .FirstAsync(g => g.TenantId == null && g.SectorKey == "construction");

        var request = new
        {
            EnglishTerm = "New Term",
            Category = "Equipment",
            IsCritical = true,
            Translations = """{"pl":"Nowy termin"}"""
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/glossary/sectors/{systemGlossary.Id}/terms", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateSystemDefaultSector_Returns400()
    {
        // Arrange: Look up the actual system default glossary (seeded by DataSeeder, ID differs from test constants)
        var context = GetDbContext();
        var systemGlossary = await context.SafetyGlossaries
            .IgnoreQueryFilters()
            .FirstAsync(g => g.TenantId == null && g.SectorKey == "construction");

        var request = new
        {
            SectorName = "Modified Name",
            SectorIcon = "new-icon"
        };

        // Act
        var response = await AdminClient.PutAsJsonAsync(
            $"/api/toolbox-talks/glossary/sectors/{systemGlossary.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteTermFromSystemDefaultSector_Returns400()
    {
        // Arrange: Look up the actual PPE term from the system default glossary
        var context = GetDbContext();
        var ppeTerm = await context.SafetyGlossaryTerms
            .IgnoreQueryFilters()
            .Include(t => t.Glossary)
            .FirstAsync(t => t.Glossary.TenantId == null
                && t.Glossary.SectorKey == "construction"
                && t.EnglishTerm == "PPE");

        // Act
        var response = await AdminClient.DeleteAsync(
            $"/api/toolbox-talks/glossary/terms/{ppeTerm.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTenantOverrideSector_Returns201_WithCopiedTerms()
    {
        // Arrange: Create a tenant override for the system default "construction" sector
        var request = new
        {
            SectorKey = TestTenantConstants.TranslationValidation.SystemGlossarySectorKey,
            SectorName = "Construction (Custom)",
            SectorIcon = "custom-hard-hat"
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync(
            "/api/toolbox-talks/glossary/sectors", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var content = await response.Content.ReadFromJsonAsync<GlossarySectorDetailDto>();
        content.Should().NotBeNull();
        content!.IsSystemDefault.Should().BeFalse();
        content.Terms.Should().NotBeEmpty(); // Should have copied system default terms
        content.Terms.Should().Contain(t => t.EnglishTerm == "PPE");
        content.Terms.Should().Contain(t => t.EnglishTerm == "Harness");
    }

    #endregion

    #region Reviewer Decisions

    [Fact]
    public async Task AcceptSection_SetsReviewerDecision()
    {
        // Arrange
        var runId = TestTenantConstants.TranslationValidation.RunWithReviewSection;

        // Act
        var response = await AdminClient.PutAsJsonAsync(
            $"{_baseTalkUrl}/runs/{runId}/sections/1/accept",
            new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify in database
        var db = GetDbContext();
        var result = await db.Set<TranslationValidationResult>()
            .IgnoreQueryFilters()
            .FirstAsync(r => r.Id == TestTenantConstants.TranslationValidation.ReviewResult);
        result.ReviewerDecision.Should().Be(ReviewerDecision.Accepted);
        result.DecisionAt.Should().NotBeNull();
        result.DecisionBy.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RejectSection_SetsReviewerDecision()
    {
        // Arrange
        var runId = TestTenantConstants.TranslationValidation.RunWithReviewSection;

        // Act
        var response = await AdminClient.PutAsJsonAsync(
            $"{_baseTalkUrl}/runs/{runId}/sections/1/reject",
            new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var db = GetDbContext();
        var result = await db.Set<TranslationValidationResult>()
            .IgnoreQueryFilters()
            .FirstAsync(r => r.Id == TestTenantConstants.TranslationValidation.ReviewResult);
        result.ReviewerDecision.Should().Be(ReviewerDecision.Rejected);
    }

    [Fact]
    public async Task EditSection_SetsEditedTranslation()
    {
        // Arrange
        var runId = TestTenantConstants.TranslationValidation.RunWithReviewSection;
        var editRequest = new { EditedTranslation = "Nie obsługuj ciężkich maszyn bez odpowiedniego szkolenia" };

        // Act
        var response = await AdminClient.PutAsJsonAsync(
            $"{_baseTalkUrl}/runs/{runId}/sections/1/edit",
            editRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var db = GetDbContext();
        var result = await db.Set<TranslationValidationResult>()
            .IgnoreQueryFilters()
            .FirstAsync(r => r.Id == TestTenantConstants.TranslationValidation.ReviewResult);
        result.ReviewerDecision.Should().Be(ReviewerDecision.Edited);
        result.EditedTranslation.Should().Be(editRequest.EditedTranslation);
    }

    [Fact]
    public async Task AcceptSection_OnAnotherTenantsRun_Returns404()
    {
        // Arrange
        var runId = TestTenantConstants.TranslationValidation.RunWithReviewSection;
        var tenantBClient = Factory.CreateAuthenticatedClient(
            TestTenantConstants.TenantB.Users.Admin.Id,
            TestTenantConstants.TenantB.Users.Admin.Email,
            TestTenantConstants.TenantB.TenantId,
            new[] { "Admin" },
            Permissions.GetAll());

        // Act
        var response = await tenantBClient.PutAsJsonAsync(
            $"{_baseTalkUrl}/runs/{runId}/sections/1/accept",
            new { });

        // Assert: 404 because the talk/run doesn't belong to tenant B
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Validation Controller Auth

    [Fact]
    public async Task WriteEndpoints_Unauthenticated_Returns401()
    {
        var runId = TestTenantConstants.TranslationValidation.CompletedRun;

        // POST validate
        await AssertUnauthorizedAsync($"{_baseTalkUrl}/validate", HttpMethod.Post);

        // PUT accept
        await AssertUnauthorizedAsync($"{_baseTalkUrl}/runs/{runId}/sections/0/accept", HttpMethod.Put);

        // PUT reject
        await AssertUnauthorizedAsync($"{_baseTalkUrl}/runs/{runId}/sections/0/reject", HttpMethod.Put);

        // PUT edit
        await AssertUnauthorizedAsync($"{_baseTalkUrl}/runs/{runId}/sections/0/edit", HttpMethod.Put);

        // DELETE run
        await AssertUnauthorizedAsync($"{_baseTalkUrl}/runs/{runId}", HttpMethod.Delete);
    }

    [Fact]
    public async Task WriteEndpoints_Operator_Returns403()
    {
        var runId = TestTenantConstants.TranslationValidation.CompletedRun;

        // POST validate
        await AssertForbiddenAsync(OperatorClient, $"{_baseTalkUrl}/validate", HttpMethod.Post);

        // PUT accept
        await AssertForbiddenAsync(OperatorClient, $"{_baseTalkUrl}/runs/{runId}/sections/0/accept", HttpMethod.Put);

        // PUT reject
        await AssertForbiddenAsync(OperatorClient, $"{_baseTalkUrl}/runs/{runId}/sections/0/reject", HttpMethod.Put);

        // PUT edit
        await AssertForbiddenAsync(OperatorClient, $"{_baseTalkUrl}/runs/{runId}/sections/0/edit", HttpMethod.Put);

        // DELETE
        await AssertForbiddenAsync(OperatorClient, $"{_baseTalkUrl}/runs/{runId}", HttpMethod.Delete);
    }

    [Fact]
    public async Task ReadEndpoints_Unauthenticated_Returns401()
    {
        var runId = TestTenantConstants.TranslationValidation.CompletedRun;

        await AssertUnauthorizedAsync($"{_baseTalkUrl}/runs");
        await AssertUnauthorizedAsync($"{_baseTalkUrl}/runs/{runId}");
    }

    [Fact]
    public async Task ReadEndpoints_Admin_Returns200()
    {
        // GET runs list
        var listResponse = await AdminClient.GetAsync($"{_baseTalkUrl}/runs?pageNumber=1&pageSize=10");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // GET run detail
        var runId = TestTenantConstants.TranslationValidation.CompletedRun;
        var detailResponse = await AdminClient.GetAsync($"{_baseTalkUrl}/runs/{runId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Safety Threshold Bump

    [Fact]
    public async Task SafetyCriticalResult_HasBumpedThreshold()
    {
        // Arrange: Read the seeded validation result that has IsSafetyCritical = true
        var db = GetDbContext();
        var result = await db.Set<TranslationValidationResult>()
            .IgnoreQueryFilters()
            .FirstAsync(r => r.Id == TestTenantConstants.TranslationValidation.PassedResult);

        // Assert: Safety-critical sections should have effective threshold = base + bump
        result.IsSafetyCritical.Should().BeTrue();
        result.EffectiveThreshold.Should().BeGreaterThan(75); // base 75 + bump 10 = 85

        // A score below effective threshold but above base threshold should be Review
        // The seeded result has FinalScore=88 which is above 85, so it passed.
        // But if we had a score of 80 (above 75 but below 85), it would be Review.
        result.EffectiveThreshold.Should().Be(85);
    }

    [Fact]
    public async Task SafetyCriticalResult_ScoreBelowEffectiveThreshold_IsReview()
    {
        // Arrange: The review result has IsSafetyCritical=true, FinalScore=69, EffectiveThreshold=85
        var db = GetDbContext();
        var result = await db.Set<TranslationValidationResult>()
            .IgnoreQueryFilters()
            .FirstAsync(r => r.Id == TestTenantConstants.TranslationValidation.ReviewResult);

        // Assert: Score 69 is below effective threshold 85 → Review outcome
        result.IsSafetyCritical.Should().BeTrue();
        result.FinalScore.Should().BeLessThan(result.EffectiveThreshold);
        result.FinalScore.Should().BeGreaterThan(75 - 15); // Above the Fail band
        result.Outcome.Should().Be(ValidationOutcome.Review);
    }

    #endregion

    #region Helper DTOs for deserialization

    private record GlossarySectorDetailDto
    {
        public Guid Id { get; init; }
        public string SectorKey { get; init; } = string.Empty;
        public string SectorName { get; init; } = string.Empty;
        public string? SectorIcon { get; init; }
        public bool IsSystemDefault { get; init; }
        public List<GlossaryTermDto> Terms { get; init; } = [];
    }

    private record GlossaryTermDto
    {
        public Guid Id { get; init; }
        public string EnglishTerm { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public bool IsCritical { get; init; }
        public string? Translations { get; init; }
    }

    #endregion
}
