using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Sectors;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Hubs;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

namespace QuantumBuild.Tests.Unit.ToolboxTalks;

/// <summary>
/// Covers the "No PDF attached" log-level fix in ContentGenerationJob.GenerateSlideshowOnlyAsync.
/// This case is reached only when a PDF-slideshow job is enqueued for a talk with no PDF — an
/// expected/preventable state (the publish-time trigger gate is the real fix), not a runtime
/// fault, so it must not log at Error (which Sentry promotes to an alert).
/// </summary>
public class ContentGenerationJobSlideshowLogLevelTests
{
    private readonly Mock<IContentGenerationService> _generationServiceMock = new();
    private readonly Mock<IHubContext<ContentGenerationHub>> _hubContextMock = new();
    private readonly Mock<ICoreDbContext> _coreDbContextMock = new();
    private readonly Mock<IToolboxTalksDbContext> _toolboxTalksDbContextMock = new();
    private readonly Mock<ISender> _senderMock = new();
    private readonly Mock<ILanguageCodeService> _languageCodeServiceMock = new();
    private readonly Mock<ISlideshowGenerationService> _slideshowGenerationServiceMock = new();
    private readonly Mock<ITenantSectorService> _tenantSectorServiceMock = new();
    private readonly Mock<ILogger<ContentGenerationJob>> _loggerMock = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _talkId = Guid.NewGuid();

    private ContentGenerationJob CreateJob() => new(
        _generationServiceMock.Object,
        _hubContextMock.Object,
        _coreDbContextMock.Object,
        _toolboxTalksDbContextMock.Object,
        _senderMock.Object,
        _languageCodeServiceMock.Object,
        _slideshowGenerationServiceMock.Object,
        _tenantSectorServiceMock.Object,
        _loggerMock.Object);

    private void SetupTalk()
    {
        var talk = ToolboxTalkBuilder.CreateBasicTalk(_talkId);
        talk.TenantId = _tenantId;
        _toolboxTalksDbContextMock
            .Setup(c => c.ToolboxTalks)
            .Returns(MockDbSetHelper.Create(talk));
    }

    private void VerifyLog(LogLevel level, Times times)
    {
        _loggerMock.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }

    [Fact]
    public async Task GenerateSlideshowOnlyAsync_NoPdfAttached_DoesNotLogAtError()
    {
        // Arrange
        SetupTalk();
        _slideshowGenerationServiceMock
            .Setup(s => s.GenerateSlideshowAsync(_tenantId, _talkId, "pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<string>("No PDF attached to this talk"));

        var job = CreateJob();

        // Act
        await job.GenerateSlideshowOnlyAsync(_talkId, _tenantId, "pdf");

        // Assert — the "no PDF" case must not raise an Error-level (Sentry-visible) log
        VerifyLog(LogLevel.Error, Times.Never());
        VerifyLog(LogLevel.Information, Times.AtLeastOnce());
    }

    [Fact]
    public async Task GenerateSlideshowOnlyAsync_GenuineFailure_StillLogsAtError()
    {
        // Arrange — a real generation failure (not the "no PDF" case) must remain Error/Sentry-visible
        SetupTalk();
        _slideshowGenerationServiceMock
            .Setup(s => s.GenerateSlideshowAsync(_tenantId, _talkId, "pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<string>("AI provider returned an empty response"));

        var job = CreateJob();

        // Act
        await job.GenerateSlideshowOnlyAsync(_talkId, _tenantId, "pdf");

        // Assert
        VerifyLog(LogLevel.Error, Times.Once());
    }
}
