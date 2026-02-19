using Hangfire;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Subtitles;
using System.Linq.Expressions;

namespace QuantumBuild.Tests.Unit.ToolboxTalks.Subtitles;

/// <summary>
/// Unit tests for SubtitleProcessingOrchestrator
/// </summary>
public class SubtitleProcessingOrchestratorTests
{
    private readonly Mock<IToolboxTalksDbContext> _dbContextMock;
    private readonly Mock<IVideoSourceProvider> _videoSourceProviderMock;
    private readonly Mock<ITranscriptionService> _transcriptionServiceMock;
    private readonly Mock<ITranslationService> _translationServiceMock;
    private readonly Mock<ISrtStorageProvider> _srtStorageProviderMock;
    private readonly Mock<ISrtGeneratorService> _srtGeneratorServiceMock;
    private readonly Mock<ILanguageCodeService> _languageCodeServiceMock;
    private readonly Mock<ISubtitleProgressReporter> _progressReporterMock;
    private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock;
    private readonly Mock<ILogger<SubtitleProcessingOrchestrator>> _loggerMock;
    private readonly IOptions<SubtitleProcessingSettings> _settings;

    private readonly List<ToolboxTalk> _toolboxTalks = new();
    private readonly List<SubtitleProcessingJob> _jobs = new();

    public SubtitleProcessingOrchestratorTests()
    {
        _dbContextMock = new Mock<IToolboxTalksDbContext>();
        _videoSourceProviderMock = new Mock<IVideoSourceProvider>();
        _transcriptionServiceMock = new Mock<ITranscriptionService>();
        _translationServiceMock = new Mock<ITranslationService>();
        _srtStorageProviderMock = new Mock<ISrtStorageProvider>();
        _srtGeneratorServiceMock = new Mock<ISrtGeneratorService>();
        _languageCodeServiceMock = new Mock<ILanguageCodeService>();
        _progressReporterMock = new Mock<ISubtitleProgressReporter>();
        _backgroundJobClientMock = new Mock<IBackgroundJobClient>();
        _loggerMock = new Mock<ILogger<SubtitleProcessingOrchestrator>>();

        _settings = Options.Create(new SubtitleProcessingSettings
        {
            BatchSize = 30,
            WordsPerSubtitle = 8
        });

        // Setup default language code mapping
        _languageCodeServiceMock.Setup(x => x.GetLanguageCodeAsync("Spanish")).ReturnsAsync("es");
        _languageCodeServiceMock.Setup(x => x.GetLanguageCodeAsync("Polish")).ReturnsAsync("pl");
        _languageCodeServiceMock.Setup(x => x.GetLanguageCodeAsync("French")).ReturnsAsync("fr");

        SetupDbContextMocks();
    }

    #region StartProcessingAsync Tests

    [Fact]
    public async Task StartProcessingAsync_WithValidTalk_CreatesJobAndQueuesBackgroundJob()
    {
        // Arrange
        var talkId = Guid.NewGuid();
        AddToolboxTalk(talkId, "Test Talk");

        var sut = CreateService();

        // Act
        var jobId = await sut.StartProcessingAsync(
            talkId,
            "https://example.com/video.mp4",
            SubtitleVideoSourceType.DirectUrl,
            new List<string> { "Spanish", "Polish" });

        // Assert
        jobId.Should().NotBeEmpty();
        _jobs.Should().HaveCount(1);
        _jobs[0].ToolboxTalkId.Should().Be(talkId);
        _jobs[0].Status.Should().Be(SubtitleProcessingStatus.Pending);
        _jobs[0].Translations.Should().HaveCount(3); // English + Spanish + Polish

        _backgroundJobClientMock.Verify(x => x.Create(
            It.IsAny<Hangfire.Common.Job>(),
            It.IsAny<IState>()), Times.Once);
    }

    [Fact]
    public async Task StartProcessingAsync_AlwaysIncludesEnglish()
    {
        // Arrange
        var talkId = Guid.NewGuid();
        AddToolboxTalk(talkId, "Test Talk");

        var sut = CreateService();

        // Act
        await sut.StartProcessingAsync(
            talkId,
            "https://example.com/video.mp4",
            SubtitleVideoSourceType.DirectUrl,
            new List<string> { "Spanish" });

        // Assert
        _jobs[0].Translations.Should().Contain(t => t.Language == "English");
        _jobs[0].Translations.Should().Contain(t => t.LanguageCode == "en");
    }

    [Fact]
    public async Task StartProcessingAsync_ExcludesDuplicateEnglish()
    {
        // Arrange
        var talkId = Guid.NewGuid();
        AddToolboxTalk(talkId, "Test Talk");

        var sut = CreateService();

        // Act
        await sut.StartProcessingAsync(
            talkId,
            "https://example.com/video.mp4",
            SubtitleVideoSourceType.DirectUrl,
            new List<string> { "English", "Spanish" }); // English explicitly passed

        // Assert
        _jobs[0].Translations.Count(t => t.Language == "English").Should().Be(1);
    }

    [Fact]
    public async Task StartProcessingAsync_WithNonExistentTalk_ThrowsException()
    {
        // Arrange
        var nonExistentTalkId = Guid.NewGuid();
        var sut = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.StartProcessingAsync(
                nonExistentTalkId,
                "https://example.com/video.mp4",
                SubtitleVideoSourceType.DirectUrl,
                new List<string> { "Spanish" }));
    }

    [Fact]
    public async Task StartProcessingAsync_WithActiveExistingJob_ThrowsException()
    {
        // Arrange
        var talkId = Guid.NewGuid();
        AddToolboxTalk(talkId, "Test Talk");
        AddExistingJob(talkId, SubtitleProcessingStatus.Transcribing);

        var sut = CreateService();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.StartProcessingAsync(
                talkId,
                "https://example.com/video.mp4",
                SubtitleVideoSourceType.DirectUrl,
                new List<string> { "Spanish" }));

        ex.Message.Should().Contain("already active");
    }

    [Fact]
    public async Task StartProcessingAsync_WithCompletedExistingJob_AllowsNewJob()
    {
        // Arrange
        var talkId = Guid.NewGuid();
        AddToolboxTalk(talkId, "Test Talk");
        AddExistingJob(talkId, SubtitleProcessingStatus.Completed);

        var sut = CreateService();

        // Act
        var jobId = await sut.StartProcessingAsync(
            talkId,
            "https://example.com/video.mp4",
            SubtitleVideoSourceType.DirectUrl,
            new List<string> { "Spanish" });

        // Assert
        jobId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task StartProcessingAsync_WithFailedExistingJob_AllowsNewJob()
    {
        // Arrange
        var talkId = Guid.NewGuid();
        AddToolboxTalk(talkId, "Test Talk");
        AddExistingJob(talkId, SubtitleProcessingStatus.Failed);

        var sut = CreateService();

        // Act
        var jobId = await sut.StartProcessingAsync(
            talkId,
            "https://example.com/video.mp4",
            SubtitleVideoSourceType.DirectUrl,
            new List<string> { "Spanish" });

        // Assert
        jobId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task StartProcessingAsync_SetsCorrectVideoSourceType()
    {
        // Arrange
        var talkId = Guid.NewGuid();
        AddToolboxTalk(talkId, "Test Talk");

        var sut = CreateService();

        // Act
        await sut.StartProcessingAsync(
            talkId,
            "https://drive.google.com/file/123",
            SubtitleVideoSourceType.GoogleDrive,
            new List<string> { "Spanish" });

        // Assert
        _jobs[0].VideoSourceType.Should().Be(SubtitleVideoSourceType.GoogleDrive);
        _jobs[0].SourceVideoUrl.Should().Be("https://drive.google.com/file/123");
    }

    [Fact]
    public async Task StartProcessingAsync_SetsStartedAtTimestamp()
    {
        // Arrange
        var talkId = Guid.NewGuid();
        AddToolboxTalk(talkId, "Test Talk");

        var sut = CreateService();

        // Act
        await sut.StartProcessingAsync(
            talkId,
            "https://example.com/video.mp4",
            SubtitleVideoSourceType.DirectUrl,
            new List<string> { "Spanish" });

        // Assert
        _jobs[0].StartedAt.Should().NotBeNull();
        _jobs[0].StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task StartProcessingAsync_SetsTranslationsPending()
    {
        // Arrange
        var talkId = Guid.NewGuid();
        AddToolboxTalk(talkId, "Test Talk");

        var sut = CreateService();

        // Act
        await sut.StartProcessingAsync(
            talkId,
            "https://example.com/video.mp4",
            SubtitleVideoSourceType.DirectUrl,
            new List<string> { "Spanish", "French" });

        // Assert
        _jobs[0].Translations.Should().OnlyContain(t => t.Status == SubtitleTranslationStatus.Pending);
    }

    #endregion

    #region GetStatusAsync Tests

    [Fact]
    public async Task GetStatusAsync_WithExistingJob_ReturnsStatus()
    {
        // Arrange
        var talkId = Guid.NewGuid();
        var job = AddExistingJob(talkId, SubtitleProcessingStatus.Translating);
        job.Translations.Add(new SubtitleTranslation
        {
            Language = "English",
            LanguageCode = "en",
            Status = SubtitleTranslationStatus.Completed,
            TotalSubtitles = 50,
            SubtitlesProcessed = 50
        });
        job.Translations.Add(new SubtitleTranslation
        {
            Language = "Spanish",
            LanguageCode = "es",
            Status = SubtitleTranslationStatus.InProgress,
            TotalSubtitles = 50,
            SubtitlesProcessed = 25
        });

        var sut = CreateService();

        // Act
        var status = await sut.GetStatusAsync(talkId);

        // Assert
        status.Should().NotBeNull();
        status!.JobId.Should().Be(job.Id);
        status.Status.Should().Be(SubtitleProcessingStatus.Translating);
        status.Languages.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetStatusAsync_WithNoJob_ReturnsNull()
    {
        // Arrange
        var nonExistentTalkId = Guid.NewGuid();
        var sut = CreateService();

        // Act
        var status = await sut.GetStatusAsync(nonExistentTalkId);

        // Assert
        status.Should().BeNull();
    }

    [Fact]
    public async Task GetStatusAsync_CalculatesPercentageCorrectly()
    {
        // Arrange
        var talkId = Guid.NewGuid();
        var job = AddExistingJob(talkId, SubtitleProcessingStatus.Completed);

        var sut = CreateService();

        // Act
        var status = await sut.GetStatusAsync(talkId);

        // Assert
        status!.OverallPercentage.Should().Be(100);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsLatestJob_WhenMultipleExist()
    {
        // Arrange
        var talkId = Guid.NewGuid();
        var oldJob = AddExistingJob(talkId, SubtitleProcessingStatus.Failed);
        oldJob.CreatedAt = DateTime.UtcNow.AddDays(-1);

        var newJob = AddExistingJob(talkId, SubtitleProcessingStatus.Transcribing);
        newJob.CreatedAt = DateTime.UtcNow;

        var sut = CreateService();

        // Act
        var status = await sut.GetStatusAsync(talkId);

        // Assert
        status!.JobId.Should().Be(newJob.Id);
    }

    [Fact]
    public async Task GetStatusAsync_CalculatesTranslatingProgressCorrectly()
    {
        // Arrange
        var talkId = Guid.NewGuid();
        var job = AddExistingJob(talkId, SubtitleProcessingStatus.Translating);
        job.Translations.Add(new SubtitleTranslation
        {
            Language = "English",
            LanguageCode = "en",
            Status = SubtitleTranslationStatus.Completed
        });
        job.Translations.Add(new SubtitleTranslation
        {
            Language = "Spanish",
            LanguageCode = "es",
            Status = SubtitleTranslationStatus.Completed
        });

        var sut = CreateService();

        // Act
        var status = await sut.GetStatusAsync(talkId);

        // Assert
        // 15 base + (2 completed / 2 total) * 80 = 95
        status!.OverallPercentage.Should().Be(95);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsCorrectCurrentStep()
    {
        // Arrange
        var talkId = Guid.NewGuid();
        var job = AddExistingJob(talkId, SubtitleProcessingStatus.Transcribing);

        var sut = CreateService();

        // Act
        var status = await sut.GetStatusAsync(talkId);

        // Assert
        status!.CurrentStep.Should().Contain("Transcribing");
    }

    [Fact]
    public async Task GetStatusAsync_IncludesLanguageProgress()
    {
        // Arrange
        var talkId = Guid.NewGuid();
        var job = AddExistingJob(talkId, SubtitleProcessingStatus.Translating);
        job.Translations.Add(new SubtitleTranslation
        {
            Language = "Spanish",
            LanguageCode = "es",
            Status = SubtitleTranslationStatus.InProgress,
            TotalSubtitles = 100,
            SubtitlesProcessed = 50,
            SrtUrl = null
        });

        var sut = CreateService();

        // Act
        var status = await sut.GetStatusAsync(talkId);

        // Assert
        var spanishLang = status!.Languages.FirstOrDefault(l => l.Language == "Spanish");
        spanishLang.Should().NotBeNull();
        spanishLang!.Percentage.Should().Be(50);
        spanishLang.LanguageCode.Should().Be("es");
    }

    #endregion

    #region ProcessAsync Tests - These require more complex mocking

    // ProcessAsync tests are more complex due to EF Core's Include() operations
    // These tests focus on verifying the orchestrator's behavior without deep EF integration

    [Fact]
    public async Task ProcessAsync_WithNonExistentJob_ReturnsWithoutError()
    {
        // Arrange
        var nonExistentJobId = Guid.NewGuid();
        var sut = CreateService();

        // Act & Assert (should not throw)
        await sut.ProcessAsync(nonExistentJobId);
    }

    #endregion

    #region Helper Methods

    private SubtitleProcessingOrchestrator CreateService()
    {
        return new SubtitleProcessingOrchestrator(
            _dbContextMock.Object,
            _videoSourceProviderMock.Object,
            _transcriptionServiceMock.Object,
            _translationServiceMock.Object,
            _srtStorageProviderMock.Object,
            _srtGeneratorServiceMock.Object,
            _languageCodeServiceMock.Object,
            _progressReporterMock.Object,
            _settings,
            _loggerMock.Object,
            _backgroundJobClientMock.Object);
    }

    private void SetupDbContextMocks()
    {
        var toolboxTalksMock = CreateMockDbSet(_toolboxTalks);
        var jobsMock = CreateMockDbSet(_jobs);

        _dbContextMock.Setup(x => x.ToolboxTalks).Returns(toolboxTalksMock.Object);
        _dbContextMock.Setup(x => x.SubtitleProcessingJobs).Returns(jobsMock.Object);
        _dbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    private Mock<DbSet<T>> CreateMockDbSet<T>(List<T> data) where T : class
    {
        var queryable = data.AsQueryable();
        var mockSet = new Mock<DbSet<T>>();

        mockSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(new TestAsyncQueryProvider<T>(queryable.Provider));
        mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
        mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
        mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(() => queryable.GetEnumerator());
        mockSet.As<IAsyncEnumerable<T>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<T>(queryable.GetEnumerator()));

        // Simulate EF Core behavior: generate ID when adding entities
        mockSet.Setup(x => x.Add(It.IsAny<T>())).Callback<T>(item =>
        {
            // Set Id if the entity has an Id property with empty Guid
            var idProperty = typeof(T).GetProperty("Id");
            if (idProperty != null && idProperty.PropertyType == typeof(Guid))
            {
                var currentId = (Guid)idProperty.GetValue(item)!;
                if (currentId == Guid.Empty)
                {
                    idProperty.SetValue(item, Guid.NewGuid());
                }
            }
            data.Add(item);
        });

        return mockSet;
    }

    private ToolboxTalk AddToolboxTalk(Guid id, string title)
    {
        var talk = new ToolboxTalk
        {
            Id = id,
            Title = title,
            IsDeleted = false
        };
        _toolboxTalks.Add(talk);
        return talk;
    }

    private SubtitleProcessingJob AddExistingJob(Guid talkId, SubtitleProcessingStatus status)
    {
        var job = new SubtitleProcessingJob
        {
            Id = Guid.NewGuid(),
            ToolboxTalkId = talkId,
            Status = status,
            SourceVideoUrl = "https://example.com/video.mp4",
            VideoSourceType = SubtitleVideoSourceType.DirectUrl,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
        _jobs.Add(job);
        return job;
    }

    #endregion

    #region Test Async Helpers

    private class TestAsyncQueryProvider<T> : IAsyncQueryProvider
    {
        private readonly IQueryProvider _inner;

        public TestAsyncQueryProvider(IQueryProvider inner) => _inner = inner;

        public IQueryable CreateQuery(Expression expression) =>
            new TestAsyncEnumerable<T>(expression);

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
            new TestAsyncEnumerable<TElement>(expression);

        public object? Execute(Expression expression) =>
            _inner.Execute(expression);

        public TResult Execute<TResult>(Expression expression) =>
            _inner.Execute<TResult>(expression);

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var resultType = typeof(TResult).GetGenericArguments()[0];
            var executionResult = typeof(IQueryProvider)
                .GetMethod(
                    name: nameof(IQueryProvider.Execute),
                    genericParameterCount: 1,
                    types: new[] { typeof(Expression) })!
                .MakeGenericMethod(resultType)
                .Invoke(this, new[] { expression });

            return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType)
                .Invoke(null, new[] { executionResult })!;
        }
    }

    private class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(Expression expression) : base(expression) { }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());

        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
    }

    private class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> _inner;

        public TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;

        public T Current => _inner.Current;

        public ValueTask<bool> MoveNextAsync() => new(_inner.MoveNext());

        public ValueTask DisposeAsync()
        {
            _inner.Dispose();
            return default;
        }
    }

    #endregion
}
