using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Core.Application.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Frameworks;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;
using QuantumBuild.Tests.Common.TestTenant;
using QuantumBuild.Tests.Integration.Setup;
using QuantumBuild.Tests.Integration.Setup.Fakes;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Characterisation tests for RequirementMappingJob's response-parse outcomes: a valid empty
/// array ("[]") is a success (one call, no Error, no rows), only an invalid response retries,
/// and a final failure logs one Error carrying ids, model and both raw responses.
/// </summary>
[Collection("Integration")]
public class RequirementMappingJobParseOutcomeTests : IntegrationTestBase
{
    public RequirementMappingJobParseOutcomeTests(CustomWebApplicationFactory factory) : base(factory) { }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<RegulatoryRequirement> CreateCandidateRequirementAsync()
    {
        var context = GetDbContext();
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            Key = $"rmp-{Guid.NewGuid():N}"[..20],
            Name = $"RMP Sector {Guid.NewGuid():N}",
            DisplayOrder = 99,
            IsActive = true
        };
        var body = new RegulatoryBody
        {
            Id = Guid.NewGuid(),
            Name = $"RMP Regulation {Guid.NewGuid():N}",
            Code = $"REG{Guid.NewGuid():N}"[..15],
            Country = "IE",
            Kind = RegulatoryBodyKind.Regulation
        };
        var doc = new RegulatoryDocument
        {
            Id = Guid.NewGuid(),
            RegulatoryBodyId = body.Id,
            Title = $"RMP Document {Guid.NewGuid():N}",
            Version = "1.0"
        };
        var profile = new RegulatoryProfile
        {
            Id = Guid.NewGuid(),
            RegulatoryDocumentId = doc.Id,
            SectorId = sector.Id,
            SectorKey = sector.Key,
            ScoreLabel = $"RMP Score {Guid.NewGuid():N}",
            ExportLabel = $"RMP{Guid.NewGuid():N}",
            Description = "Integration test profile",
            IsActive = true
        };
        var requirement = new RegulatoryRequirement
        {
            Id = Guid.NewGuid(),
            RegulatoryProfileId = profile.Id,
            Title = $"RMP Requirement {Guid.NewGuid():N}",
            Description = "Integration test requirement",
            Priority = "med",
            IngestionStatus = RequirementIngestionStatus.Approved,
            IsActive = true
        };
        context.Sectors.Add(sector);
        context.TenantSectors.Add(new TenantSector
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantConstants.TenantId,
            SectorId = sector.Id,
            IsDefault = false
        });
        context.RegulatoryBodies.Add(body);
        context.RegulatoryDocuments.Add(doc);
        context.RegulatoryProfiles.Add(profile);
        context.RegulatoryRequirements.Add(requirement);
        await context.SaveChangesAsync();
        return requirement;
    }

    private async Task<Guid> CreateTalkAsync()
    {
        var talkId = Guid.NewGuid();
        var context = GetDbContext();
        context.Set<ToolboxTalk>().Add(new ToolboxTalk
        {
            Id = talkId,
            TenantId = TestTenantConstants.TenantId,
            Code = $"RMP{Guid.NewGuid():N}"[..8],
            Title = $"RMP Parse Outcome Talk {Guid.NewGuid():N}",
            Description = "Integration test talk for parse-outcome tests",
            Frequency = ToolboxTalkFrequency.Once,
            VideoSource = VideoSource.None,
            MinimumVideoWatchPercent = 90,
            RequiresQuiz = false,
            IsActive = true,
            GenerateCertificate = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        context.Set<ToolboxTalkSection>().Add(new ToolboxTalkSection
        {
            Id = Guid.NewGuid(),
            ToolboxTalkId = talkId,
            SectionNumber = 1,
            Title = "Section 1",
            Content = "<p>Hand washing content.</p>",
            RequiresAcknowledgment = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        await context.SaveChangesAsync();
        return talkId;
    }

    private async Task<Guid> CreateCourseAsync()
    {
        var courseId = Guid.NewGuid();
        var context = GetDbContext();
        context.ToolboxTalkCourses.Add(new ToolboxTalkCourse
        {
            Id = courseId,
            TenantId = TestTenantConstants.TenantId,
            Title = $"RMP Parse Outcome Course {Guid.NewGuid():N}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        await context.SaveChangesAsync();
        return courseId;
    }

    private (RequirementMappingJob Job, FakeAnthropicHttpMessageHandler Handler, CapturingLogger<RequirementMappingJob> Logger)
        BuildJob(IServiceScope scope, Func<int, string> responseForCall)
    {
        var callCount = 0;
        var handler = new FakeAnthropicHttpMessageHandler
        {
            Responder = _ => (responseForCall(++callCount), null)
        };
        var logger = new CapturingLogger<RequirementMappingJob>();

        var job = new RequirementMappingJob(
            scope.ServiceProvider.GetRequiredService<IToolboxTalksDbContext>(),
            new HttpClient(handler),
            Options.Create(new SubtitleProcessingSettings
            {
                Claude = new QuantumBuild.Core.Application.Abstractions.AI.ClaudeSettings
                {
                    BaseUrl = "https://fake-claude.test",
                    ApiKey = "test-key"
                }
            }),
            scope.ServiceProvider.GetRequiredService<IAiUsageLogger>(),
            scope.ServiceProvider.GetRequiredService<IApplicableFrameworksService>(),
            logger,
            Options.Create(new AIProviderOptions
            {
                Anthropic = new AnthropicProviderOptions
                {
                    Models = new AnthropicModels { Sonnet = "claude-sonnet-test", Haiku = "claude-haiku-test" }
                }
            }));

        return (job, handler, logger);
    }

    private async Task<int> CountTalkMappingsAsync(Guid talkId) =>
        await GetDbContext().RegulatoryRequirementMappings.IgnoreQueryFilters()
            .CountAsync(m => m.ToolboxTalkId == talkId);

    private static string ValidSuggestion(Guid requirementId) =>
        $$"""[{"requirementId":"{{requirementId}}","confidenceScore":90,"reasoning":"Covers it."}]""";

    // ── tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EmptyArrayFirstResponse_IsSuccess_OneCall_NoError_NoRows()
    {
        await CreateCandidateRequirementAsync();
        var talkId = await CreateTalkAsync();

        using var scope = Factory.Services.CreateScope();
        var (job, handler, logger) = BuildJob(scope, _ => "[]");
        await job.MapRequirementsAsync(TestTenantConstants.TenantId, talkId, null);

        handler.RequestBodies.Should().HaveCount(1);
        logger.Entries.Should().NotContain(e => e.Level == LogLevel.Error);
        logger.Entries.Should().Contain(e => e.Level == LogLevel.Information
            && e.Message.Contains("No requirements matched")
            && e.Message.Contains(talkId.ToString())
            && e.Message.Contains(TestTenantConstants.TenantId.ToString()));
        (await CountTalkMappingsAsync(talkId)).Should().Be(0);
    }

    [Fact]
    public async Task InvalidThenEmptyArray_IsSuccess_TwoCalls_NoError()
    {
        await CreateCandidateRequirementAsync();
        var talkId = await CreateTalkAsync();

        using var scope = Factory.Services.CreateScope();
        var (job, handler, logger) = BuildJob(scope, n => n == 1 ? "Here are the mappings: none" : "[]");
        await job.MapRequirementsAsync(TestTenantConstants.TenantId, talkId, null);

        handler.RequestBodies.Should().HaveCount(2);
        logger.Entries.Should().NotContain(e => e.Level == LogLevel.Error);
        logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning
            && e.Message.Contains("Here are the mappings: none"));
        (await CountTalkMappingsAsync(talkId)).Should().Be(0);
    }

    [Fact]
    public async Task BothInvalid_LogsSingleError_WithIdsModelAndBothResponses()
    {
        await CreateCandidateRequirementAsync();
        var talkId = await CreateTalkAsync();

        using var scope = Factory.Services.CreateScope();
        var (job, handler, logger) = BuildJob(scope,
            n => n == 1 ? "FIRST-BAD-RESPONSE" : "SECOND-BAD-RESPONSE");
        await job.MapRequirementsAsync(TestTenantConstants.TenantId, talkId, null);

        handler.RequestBodies.Should().HaveCount(2);
        var errors = logger.Entries.Where(e => e.Level == LogLevel.Error).ToList();
        errors.Should().ContainSingle();
        errors[0].Message.Should().Contain(talkId.ToString());
        errors[0].Message.Should().Contain(TestTenantConstants.TenantId.ToString());
        errors[0].Message.Should().Contain("claude-sonnet-test");
        errors[0].Message.Should().Contain("FIRST-BAD-RESPONSE");
        errors[0].Message.Should().Contain("SECOND-BAD-RESPONSE");
        (await CountTalkMappingsAsync(talkId)).Should().Be(0);
    }

    [Fact]
    public async Task JsonExceptionFirstAttempt_LogsExactlyOneWarning_WithReasonAndResponse()
    {
        await CreateCandidateRequirementAsync();
        var talkId = await CreateTalkAsync();

        // A bare object where an array is expected makes System.Text.Json throw a JsonException
        const string expectedReason = "could not be converted";

        using var scope = Factory.Services.CreateScope();
        var (job, _, logger) = BuildJob(scope, n => n == 1 ? "{not json" : "[]");
        await job.MapRequirementsAsync(TestTenantConstants.TenantId, talkId, null);

        var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        warnings.Should().ContainSingle();
        warnings[0].Message.Should().Contain(expectedReason);
        warnings[0].Message.Should().Contain("{not json");
    }

    [Fact]
    public async Task BothInvalid_Error_ContainsBothReasons()
    {
        await CreateCandidateRequirementAsync();
        var talkId = await CreateTalkAsync();

        using var scope = Factory.Services.CreateScope();
        // First: blank. Second: malformed JSON.
        var (job, _, logger) = BuildJob(scope, n => n == 1 ? "   " : "{second-bad");
        await job.MapRequirementsAsync(TestTenantConstants.TenantId, talkId, null);

        var errors = logger.Entries.Where(e => e.Level == LogLevel.Error).ToList();
        errors.Should().ContainSingle();
        errors[0].Message.Should().Contain("FirstReason: blank");
        errors[0].Message.Should().Contain("{second-bad");
        errors[0].Message.Should().Contain("RetryReason: ");
        errors[0].Message.Should().NotContain("RetryReason: blank");
    }

    [Fact]
    public async Task ValidSuggestions_PersistRows()
    {
        var requirement = await CreateCandidateRequirementAsync();
        var talkId = await CreateTalkAsync();

        using var scope = Factory.Services.CreateScope();
        var (job, handler, logger) = BuildJob(scope, _ => ValidSuggestion(requirement.Id));
        await job.MapRequirementsAsync(TestTenantConstants.TenantId, talkId, null);

        handler.RequestBodies.Should().HaveCount(1);
        logger.Entries.Should().NotContain(e => e.Level == LogLevel.Error);
        var mappings = await GetDbContext().RegulatoryRequirementMappings.IgnoreQueryFilters()
            .Where(m => m.ToolboxTalkId == talkId).ToListAsync();
        mappings.Should().ContainSingle();
        mappings[0].RegulatoryRequirementId.Should().Be(requirement.Id);
        mappings[0].MappingStatus.Should().Be(RequirementMappingStatus.Suggested);
    }

    [Fact]
    public async Task SecondRunOnSameTalk_DoesNotDuplicateRows()
    {
        var requirement = await CreateCandidateRequirementAsync();
        var talkId = await CreateTalkAsync();

        for (var run = 0; run < 2; run++)
        {
            using var scope = Factory.Services.CreateScope();
            var (job, _, _) = BuildJob(scope, _ => ValidSuggestion(requirement.Id));
            await job.MapRequirementsAsync(TestTenantConstants.TenantId, talkId, null);
        }

        (await CountTalkMappingsAsync(talkId)).Should().Be(1);
    }

    [Fact]
    public async Task Course_EmptyArrayFirstResponse_IsSuccess_OneCall_NoError_NoRows()
    {
        await CreateCandidateRequirementAsync();
        var courseId = await CreateCourseAsync();

        using var scope = Factory.Services.CreateScope();
        var (job, handler, logger) = BuildJob(scope, _ => "[]");
        await job.MapRequirementsAsync(TestTenantConstants.TenantId, null, courseId);

        handler.RequestBodies.Should().HaveCount(1);
        logger.Entries.Should().NotContain(e => e.Level == LogLevel.Error);
        logger.Entries.Should().Contain(e => e.Message.Contains("No requirements matched")
            && e.Message.Contains(courseId.ToString()));
        (await GetDbContext().RegulatoryRequirementMappings.IgnoreQueryFilters()
            .CountAsync(m => m.CourseId == courseId)).Should().Be(0);
    }
}
