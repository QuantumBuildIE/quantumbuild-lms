using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.Setup.Fakes;

/// <summary>
/// Fake transcription service for testing that returns predefined results.
/// </summary>
public class FakeTranscriptionService : ITranscriptionService
{
    private bool _shouldFail;
    private string _failureMessage = "Transcription failed in test";
    private List<TranscriptWord> _words = GenerateDefaultWords();

    /// <summary>
    /// Configure the service to fail on next call.
    /// </summary>
    public void SetShouldFail(bool shouldFail, string? message = null)
    {
        _shouldFail = shouldFail;
        if (message != null)
            _failureMessage = message;
    }

    /// <summary>
    /// Set custom words to return.
    /// </summary>
    public void SetWords(List<TranscriptWord> words)
    {
        _words = words;
    }

    /// <summary>
    /// Reset to default state.
    /// </summary>
    public void Reset()
    {
        _shouldFail = false;
        _failureMessage = "Transcription failed in test";
        _words = GenerateDefaultWords();
    }

    public Task<TranscriptionResult> TranscribeAsync(string videoUrl, CancellationToken cancellationToken = default)
    {
        if (_shouldFail)
        {
            return Task.FromResult(TranscriptionResult.FailureResult(_failureMessage));
        }

        return Task.FromResult(TranscriptionResult.SuccessResult(_words));
    }

    private static List<TranscriptWord> GenerateDefaultWords()
    {
        return new List<TranscriptWord>
        {
            new() { Text = "Welcome", Type = "word", Start = 0.0m, End = 0.5m },
            new() { Text = " ", Type = "spacing", Start = 0.5m, End = 0.5m },
            new() { Text = "to", Type = "word", Start = 0.5m, End = 0.7m },
            new() { Text = " ", Type = "spacing", Start = 0.7m, End = 0.7m },
            new() { Text = "the", Type = "word", Start = 0.7m, End = 0.9m },
            new() { Text = " ", Type = "spacing", Start = 0.9m, End = 0.9m },
            new() { Text = "safety", Type = "word", Start = 0.9m, End = 1.3m },
            new() { Text = " ", Type = "spacing", Start = 1.3m, End = 1.3m },
            new() { Text = "briefing", Type = "word", Start = 1.3m, End = 1.8m },
            new() { Text = ".", Type = "punctuation", Start = 1.8m, End = 1.8m }
        };
    }
}

/// <summary>
/// Fake translation service for testing that returns predefined results.
/// </summary>
public class FakeTranslationService : ITranslationService
{
    private bool _shouldFail;
    private string _failureMessage = "Translation failed in test";
    private readonly Dictionary<string, string> _translations = new();

    /// <summary>
    /// Configure the service to fail on next call.
    /// </summary>
    public void SetShouldFail(bool shouldFail, string? message = null)
    {
        _shouldFail = shouldFail;
        if (message != null)
            _failureMessage = message;
    }

    /// <summary>
    /// Set a custom translation for a specific language.
    /// </summary>
    public void SetTranslation(string language, string translation)
    {
        _translations[language] = translation;
    }

    /// <summary>
    /// Reset to default state.
    /// </summary>
    public void Reset()
    {
        _shouldFail = false;
        _failureMessage = "Translation failed in test";
        _translations.Clear();
    }

    public Task<TranslationResult> TranslateSrtBatchAsync(
        string srtContent,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        if (_shouldFail)
        {
            return Task.FromResult(TranslationResult.FailureResult(_failureMessage));
        }

        if (_translations.TryGetValue(targetLanguage, out var translation))
        {
            return Task.FromResult(TranslationResult.SuccessResult(translation));
        }

        // Generate fake translation by adding language prefix
        var fakeTranslation = $"[{targetLanguage}] {srtContent}";
        return Task.FromResult(TranslationResult.SuccessResult(fakeTranslation));
    }
}

/// <summary>
/// Fake SRT storage provider for testing.
/// </summary>
public class FakeSrtStorageProvider : ISrtStorageProvider
{
    private bool _shouldFail;
    private string _failureMessage = "Storage operation failed in test";
    private readonly Dictionary<string, string> _storedFiles = new();

    /// <summary>
    /// Gets the files that have been stored.
    /// </summary>
    public IReadOnlyDictionary<string, string> StoredFiles => _storedFiles;

    /// <summary>
    /// Configure the service to fail on next call.
    /// </summary>
    public void SetShouldFail(bool shouldFail, string? message = null)
    {
        _shouldFail = shouldFail;
        if (message != null)
            _failureMessage = message;
    }

    /// <summary>
    /// Reset to default state and clear stored files.
    /// </summary>
    public void Reset()
    {
        _shouldFail = false;
        _failureMessage = "Storage operation failed in test";
        _storedFiles.Clear();
    }

    public Task<SrtUploadResult> UploadSrtAsync(
        string srtContent,
        string fileName,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (_shouldFail)
        {
            return Task.FromResult(SrtUploadResult.FailureResult(_failureMessage));
        }

        // Store with tenant-prefixed key to match real behavior
        var key = $"{tenantId}/{fileName}";
        _storedFiles[key] = srtContent;
        var url = $"https://fake-storage.test/{tenantId}/{fileName}";
        return Task.FromResult(SrtUploadResult.SuccessResult(url));
    }

    public Task<string?> GetSrtContentAsync(string fileName, CancellationToken cancellationToken = default)
    {
        _storedFiles.TryGetValue(fileName, out var content);
        return Task.FromResult(content);
    }

    public Task<bool> DeleteSrtAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (_shouldFail)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(_storedFiles.Remove(fileName));
    }
}

/// <summary>
/// Fake video source provider for testing.
/// </summary>
public class FakeVideoSourceProvider : IVideoSourceProvider
{
    private bool _shouldFail;
    private string _failureMessage = "Video source operation failed in test";
    private string _directUrl = "https://fake-video.test/video.mp4";

    public bool SupportsUpload => true;

    /// <summary>
    /// Configure the service to fail on next call.
    /// </summary>
    public void SetShouldFail(bool shouldFail, string? message = null)
    {
        _shouldFail = shouldFail;
        if (message != null)
            _failureMessage = message;
    }

    /// <summary>
    /// Set the direct URL to return.
    /// </summary>
    public void SetDirectUrl(string url)
    {
        _directUrl = url;
    }

    /// <summary>
    /// Reset to default state.
    /// </summary>
    public void Reset()
    {
        _shouldFail = false;
        _failureMessage = "Video source operation failed in test";
        _directUrl = "https://fake-video.test/video.mp4";
    }

    public Task<VideoSourceResult> GetDirectUrlAsync(
        string sourceUrl,
        SubtitleVideoSourceType sourceType,
        CancellationToken cancellationToken = default)
    {
        if (_shouldFail)
        {
            return Task.FromResult(VideoSourceResult.FailureResult(_failureMessage));
        }

        return Task.FromResult(VideoSourceResult.SuccessResult(_directUrl));
    }

    public Task<VideoUploadResult> UploadVideoAsync(
        Stream videoStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (_shouldFail)
        {
            return Task.FromResult(VideoUploadResult.FailureResult(_failureMessage));
        }

        var url = $"https://fake-video.test/{fileName}";
        return Task.FromResult(VideoUploadResult.SuccessResult(url, fileName));
    }
}

/// <summary>
/// Fake subtitle progress reporter for testing that captures progress updates.
/// </summary>
public class FakeSubtitleProgressReporter : ISubtitleProgressReporter
{
    private readonly List<(Guid JobId, SubtitleProgressUpdate Update)> _progressUpdates = new();

    /// <summary>
    /// Gets all progress updates that have been reported.
    /// </summary>
    public IReadOnlyList<(Guid JobId, SubtitleProgressUpdate Update)> ProgressUpdates => _progressUpdates;

    /// <summary>
    /// Gets the last progress update for a specific job.
    /// </summary>
    public SubtitleProgressUpdate? GetLastUpdateForJob(Guid jobId)
    {
        return _progressUpdates.LastOrDefault(u => u.JobId == jobId).Update;
    }

    /// <summary>
    /// Clear all recorded updates.
    /// </summary>
    public void Clear()
    {
        _progressUpdates.Clear();
    }

    public Task ReportProgressAsync(Guid jobId, SubtitleProgressUpdate update, CancellationToken cancellationToken = default)
    {
        _progressUpdates.Add((jobId, update));
        return Task.CompletedTask;
    }
}
