using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Core.Infrastructure.Identity;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for the 9 learning-wizard toggle tenant defaults (Chunk A):
/// DefaultVideoRightsConfirmed, DefaultUseQuestionPool, DefaultGenerateSlideshow,
/// DefaultAutoAssign, DefaultPreserveSourceWording, DefaultShuffleQuestions,
/// DefaultShuffleOptions, DefaultIncludeQuiz, DefaultAllowRetry.
///
/// Covers: GET /api/toolbox-talks/settings, PUT /api/toolbox-talks/settings, and their
/// inheritance into POST /api/toolbox-talks/initialise (InitialiseToolboxTalkCommandHandler).
///
/// DefaultVideoRightsConfirmed is intentionally excluded from creation-inheritance
/// assertions: videoRightsConfirmed is a client-side-only wizard confirmation gate with no
/// corresponding ToolboxTalk entity property (never sent to the backend) — see Chunk A report.
/// </summary>
[Collection("Integration")]
public class LearningWizardToggleTenantDefaultsTests : IntegrationTestBase
{
    public LearningWizardToggleTenantDefaultsTests(CustomWebApplicationFactory factory)
        : base(factory) { }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string UniqueTitle(string prefix = "Toggle Defaults Talk") =>
        $"{prefix} {Guid.NewGuid():N}"[..Math.Min(80, prefix.Length + 33)];

    /// <summary>
    /// Builds a PUT /settings body. The three wizard-default base fields carry valid
    /// values required by UpdateToolboxTalkTenantDefaultsCommandValidator; the 9 new
    /// toggle fields are passed through as-is (null = omitted from the update).
    /// </summary>
    private static object TenantDefaultsBody(
        bool? defaultVideoRightsConfirmed = null,
        bool? defaultUseQuestionPool = null,
        bool? defaultGenerateSlideshow = null,
        bool? defaultAutoAssign = null,
        bool? defaultPreserveSourceWording = null,
        bool? defaultShuffleQuestions = null,
        bool? defaultShuffleOptions = null,
        bool? defaultIncludeQuiz = null,
        bool? defaultAllowRetry = null) => new
    {
        DefaultMinimumVideoWatchPercent = 90,
        DefaultAutoAssignDueDays = 14,
        DefaultGenerateCertificate = true,
        DefaultRefresherFrequency = "Once",
        DefaultIsActive = true,
        DefaultVideoRightsConfirmed = defaultVideoRightsConfirmed,
        DefaultUseQuestionPool = defaultUseQuestionPool,
        DefaultGenerateSlideshow = defaultGenerateSlideshow,
        DefaultAutoAssign = defaultAutoAssign,
        DefaultPreserveSourceWording = defaultPreserveSourceWording,
        DefaultShuffleQuestions = defaultShuffleQuestions,
        DefaultShuffleOptions = defaultShuffleOptions,
        DefaultIncludeQuiz = defaultIncludeQuiz,
        DefaultAllowRetry = defaultAllowRetry,
    };

    private static object MinimalInitialiseRequest(string title, bool? preserveSourceWording = null, bool? includeQuiz = null) => new
    {
        Title = title,
        InputMode = "Text",
        SourceLanguageCode = "en",
        SourceText = "This is the source content for the learning.",
        TargetLanguageCodes = new[] { "fr" },
        AudienceRole = "Operator",
        PreserveSourceWording = preserveSourceWording,
        IncludeQuiz = includeQuiz,
    };

    private async Task<ToolboxTalkSettingsResponseDto> GetSettingsAsync(HttpClient? client = null)
    {
        var response = await (client ?? AdminClient).GetAsync("/api/toolbox-talks/settings");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<ToolboxTalkSettingsResponseDto>>();
        return result!.Data!;
    }

    private async Task<ToolboxTalkSettingsResponseDto> UpdateSettingsAsync(object body)
    {
        var response = await AdminClient.PutAsJsonAsync("/api/toolbox-talks/settings", body);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<ToolboxTalkSettingsResponseDto>>();
        return result!.Data!;
    }

    private async Task<Guid> InitialiseTalkAsync(object request)
    {
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/initialise", request);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<InitialisedTalkDto>();
        return dto!.Id;
    }

    private async Task<ToolboxTalk> GetTalkFromDbAsync(Guid id)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var talk = await db.Set<ToolboxTalk>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
        talk.Should().NotBeNull();
        return talk!;
    }

    // ── 1. New tenant defaults ───────────────────────────────────────────────

    // Uses Tenant B rather than the shared primary AdminClient tenant: the toolbox_talks
    // schema is excluded from Respawner's per-test reset (see CustomWebApplicationFactory —
    // SchemasToInclude = ["public"] only), so ToolboxTalkSettings rows persist for the whole
    // test run. Tenant B is never seeded a settings row and no other test mutates it, so it
    // deterministically exercises GetToolboxTalkSettingsQueryHandler's "no row exists"
    // fallback — the true behaviour of a brand-new, never-configured tenant.
    [Fact]
    public async Task NewTenant_AllNineToggleDefaults_AtExpectedInitialValues()
    {
        var tenantBClient = Factory.CreateAuthenticatedClient(
            TestTenantConstants.TenantB.Users.Admin.Id,
            TestTenantConstants.TenantB.Users.Admin.Email,
            TestTenantConstants.TenantB.TenantId,
            new[] { "Admin" },
            Permissions.GetAll());

        var settings = await GetSettingsAsync(tenantBClient);

        // Three false
        settings.DefaultVideoRightsConfirmed.Should().BeFalse();
        settings.DefaultUseQuestionPool.Should().BeFalse();
        settings.DefaultGenerateSlideshow.Should().BeFalse();

        // Six true
        settings.DefaultAutoAssign.Should().BeTrue();
        settings.DefaultPreserveSourceWording.Should().BeTrue();
        settings.DefaultShuffleQuestions.Should().BeTrue();
        settings.DefaultShuffleOptions.Should().BeTrue();
        settings.DefaultIncludeQuiz.Should().BeTrue();
        settings.DefaultAllowRetry.Should().BeTrue();
    }

    // ── 2. Updating tenant settings persists ─────────────────────────────────

    [Fact]
    public async Task UpdateTenantDefaults_PersistsAllNineNewValues()
    {
        var updated = await UpdateSettingsAsync(TenantDefaultsBody(
            defaultVideoRightsConfirmed: true,
            defaultUseQuestionPool: true,
            defaultGenerateSlideshow: true,
            defaultAutoAssign: false,
            defaultPreserveSourceWording: false,
            defaultShuffleQuestions: false,
            defaultShuffleOptions: false,
            defaultIncludeQuiz: false,
            defaultAllowRetry: false));

        updated.DefaultVideoRightsConfirmed.Should().BeTrue();
        updated.DefaultUseQuestionPool.Should().BeTrue();
        updated.DefaultGenerateSlideshow.Should().BeTrue();
        updated.DefaultAutoAssign.Should().BeFalse();
        updated.DefaultPreserveSourceWording.Should().BeFalse();
        updated.DefaultShuffleQuestions.Should().BeFalse();
        updated.DefaultShuffleOptions.Should().BeFalse();
        updated.DefaultIncludeQuiz.Should().BeFalse();
        updated.DefaultAllowRetry.Should().BeFalse();

        // Re-fetch to confirm persistence, not just the echoed response
        var refetched = await GetSettingsAsync();
        refetched.DefaultAutoAssign.Should().BeFalse();
        refetched.DefaultShuffleQuestions.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateTenantDefaults_OmittedToggle_PreservesExistingValue()
    {
        // First call sets DefaultAutoAssign = false explicitly
        await UpdateSettingsAsync(TenantDefaultsBody(defaultAutoAssign: false));

        // Second call omits DefaultAutoAssign (null) but sets a different toggle
        var updated = await UpdateSettingsAsync(TenantDefaultsBody(defaultShuffleQuestions: false));

        updated.DefaultAutoAssign.Should().BeFalse("omitted fields must preserve the previously stored value");
        updated.DefaultShuffleQuestions.Should().BeFalse();
    }

    // ── 3. New learning inherits tenant defaults (all 8 entity-backed toggles) ──

    [Fact]
    public async Task CreateLearning_InheritsAllEightEntityBackedTenantDefaults()
    {
        // Flip every entity-backed toggle away from its initial value
        await UpdateSettingsAsync(TenantDefaultsBody(
            defaultUseQuestionPool: true,
            defaultGenerateSlideshow: true,
            defaultAutoAssign: false,
            defaultPreserveSourceWording: false,
            defaultShuffleQuestions: false,
            defaultShuffleOptions: false,
            defaultIncludeQuiz: false,
            defaultAllowRetry: false));

        // PreserveSourceWording/IncludeQuiz omitted from the request → must fall back to tenant defaults
        var talkId = await InitialiseTalkAsync(MinimalInitialiseRequest(UniqueTitle("Inherit All Toggles")));
        var talk = await GetTalkFromDbAsync(talkId);

        talk.UseQuestionPool.Should().BeTrue();
        talk.GenerateSlidesFromPdf.Should().BeTrue();
        talk.AutoAssignToNewEmployees.Should().BeFalse();
        talk.PreserveSourceWording.Should().BeFalse();
        talk.ShuffleQuestions.Should().BeFalse();
        talk.ShuffleOptions.Should().BeFalse();
        talk.RequiresQuiz.Should().BeFalse();
        talk.AllowRetry.Should().BeFalse();
    }

    // ── 4. Snapshot rule: changing tenant default after creation doesn't retroact ──

    // The toolbox_talks schema is excluded from Respawner's per-test reset (see note on
    // NewTenant_AllNineToggleDefaults_AtExpectedInitialValues above), so this test cannot
    // assume the shared tenant's settings are at their freshly-seeded initial values — it
    // sets a known starting state explicitly before asserting the snapshot rule.
    [Fact]
    public async Task ChangingTenantDefaultAfterCreation_DoesNotAffectExistingLearning()
    {
        await UpdateSettingsAsync(TenantDefaultsBody(
            defaultAutoAssign: true,
            defaultShuffleQuestions: true,
            defaultUseQuestionPool: false));

        var talkId = await InitialiseTalkAsync(MinimalInitialiseRequest(UniqueTitle("Snapshot Rule Talk")));
        var talkBefore = await GetTalkFromDbAsync(talkId);
        talkBefore.AutoAssignToNewEmployees.Should().BeTrue();
        talkBefore.ShuffleQuestions.Should().BeTrue();
        talkBefore.UseQuestionPool.Should().BeFalse();

        // Now flip the tenant defaults
        await UpdateSettingsAsync(TenantDefaultsBody(
            defaultAutoAssign: false,
            defaultShuffleQuestions: false,
            defaultUseQuestionPool: true));

        // The already-created talk must be unaffected (creation-time snapshot)
        var talkAfter = await GetTalkFromDbAsync(talkId);
        talkAfter.AutoAssignToNewEmployees.Should().BeTrue();
        talkAfter.ShuffleQuestions.Should().BeTrue();
        talkAfter.UseQuestionPool.Should().BeFalse();

        // A newly created talk, however, must pick up the new tenant defaults
        var newTalkId = await InitialiseTalkAsync(MinimalInitialiseRequest(UniqueTitle("Post Change Talk")));
        var newTalk = await GetTalkFromDbAsync(newTalkId);
        newTalk.AutoAssignToNewEmployees.Should().BeFalse();
        newTalk.ShuffleQuestions.Should().BeFalse();
        newTalk.UseQuestionPool.Should().BeTrue();
    }

    // ── 5. Explicit per-learning override wins over tenant default ──────────────

    [Fact]
    public async Task ExplicitOverrideInInitialiseCommand_WinsOverTenantDefault()
    {
        // Tenant defaults both to false
        await UpdateSettingsAsync(TenantDefaultsBody(
            defaultPreserveSourceWording: false,
            defaultIncludeQuiz: false));

        // Caller explicitly requests true for both, despite the tenant default being false
        var talkId = await InitialiseTalkAsync(
            MinimalInitialiseRequest(UniqueTitle("Explicit Override Talk"), preserveSourceWording: true, includeQuiz: true));

        var talk = await GetTalkFromDbAsync(talkId);
        talk.PreserveSourceWording.Should().BeTrue();
        talk.RequiresQuiz.Should().BeTrue();
    }

    [Fact]
    public async Task OmittedOverrideInInitialiseCommand_FallsBackToTenantDefault()
    {
        // Tenant defaults both to false
        await UpdateSettingsAsync(TenantDefaultsBody(
            defaultPreserveSourceWording: false,
            defaultIncludeQuiz: false));

        // Caller omits both fields entirely (null) — should fall back to the tenant default (false),
        // not the system-wide initial default (true).
        var talkId = await InitialiseTalkAsync(MinimalInitialiseRequest(UniqueTitle("Omitted Override Talk")));

        var talk = await GetTalkFromDbAsync(talkId);
        talk.PreserveSourceWording.Should().BeFalse();
        talk.RequiresQuiz.Should().BeFalse();
    }

    // ── local DTOs ────────────────────────────────────────────────────────────

    private record InitialisedTalkDto(Guid Id);

    private record ResultWrapper<T>(bool Success, T? Data, string? Message, List<string>? Errors);

    private record ToolboxTalkSettingsResponseDto(
        Guid Id,
        bool DefaultVideoRightsConfirmed,
        bool DefaultUseQuestionPool,
        bool DefaultGenerateSlideshow,
        bool DefaultAutoAssign,
        bool DefaultPreserveSourceWording,
        bool DefaultShuffleQuestions,
        bool DefaultShuffleOptions,
        bool DefaultIncludeQuiz,
        bool DefaultAllowRetry);
}
