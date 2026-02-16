using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Subtitles;

/// <summary>
/// Transcription service implementation using ElevenLabs Speech-to-Text API.
/// Converts video audio to word-level transcription with timing data.
/// </summary>
public class ElevenLabsTranscriptionService : ITranscriptionService
{
    private readonly HttpClient _httpClient;
    private readonly SubtitleProcessingSettings _settings;
    private readonly ILogger<ElevenLabsTranscriptionService> _logger;

    public ElevenLabsTranscriptionService(
        HttpClient httpClient,
        IOptions<SubtitleProcessingSettings> settings,
        ILogger<ElevenLabsTranscriptionService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Transcribes audio from a video URL using ElevenLabs API.
    /// Downloads the video first, then uploads it to ElevenLabs as a file.
    /// </summary>
    public async Task<TranscriptionResult> TranscribeAsync(string videoUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting transcription for video: {VideoUrl}", videoUrl);

            // Step 1: Download the video from the URL
            _logger.LogInformation("Downloading video from: {VideoUrl}", videoUrl);

            using var downloadResponse = await _httpClient.GetAsync(videoUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!downloadResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to download video. Status: {StatusCode}", downloadResponse.StatusCode);
                return TranscriptionResult.FailureResult($"Failed to download video from {videoUrl}. Status: {downloadResponse.StatusCode}");
            }

            var videoBytes = await downloadResponse.Content.ReadAsByteArrayAsync(cancellationToken);
            _logger.LogInformation("Video downloaded successfully. Size: {Size} bytes ({SizeMB:F2} MB)",
                videoBytes.Length, videoBytes.Length / 1024.0 / 1024.0);

            if (videoBytes.Length == 0)
            {
                _logger.LogError("Downloaded video is empty (0 bytes)");
                return TranscriptionResult.FailureResult("Downloaded video is empty (0 bytes)");
            }

            // Step 2: Extract filename from URL
            var fileName = GetFileNameFromUrl(videoUrl) ?? "video.mp4";
            _logger.LogInformation("Using filename: {FileName}", fileName);

            // Step 3: Determine content type based on file extension
            var contentType = GetContentType(fileName);

            // Step 4: Upload to ElevenLabs as file (not URL)
            _logger.LogInformation("Uploading to ElevenLabs for transcription. Model: {Model}", _settings.ElevenLabs.Model);

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.ElevenLabs.BaseUrl}/speech-to-text");
            request.Headers.Add("xi-api-key", _settings.ElevenLabs.ApiKey);

            var fileContent = new ByteArrayContent(videoBytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

            var formContent = new MultipartFormDataContent
            {
                { fileContent, "file", fileName },
                { new StringContent(_settings.ElevenLabs.Model), "model_id" }
            };

            request.Content = formContent;

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("ElevenLabs API error: {StatusCode} - {Response}", response.StatusCode, responseBody);
                return TranscriptionResult.FailureResult($"ElevenLabs API error: {response.StatusCode} - {responseBody}");
            }

            _logger.LogInformation("ElevenLabs transcription completed successfully");

            var words = ParseTranscriptionResponse(responseBody);

            _logger.LogInformation("Transcription completed. Words extracted: {WordCount}", words.Count);

            return TranscriptionResult.SuccessResult(words, responseBody);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during transcription for video: {VideoUrl}", videoUrl);
            return TranscriptionResult.FailureResult($"HTTP request failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse transcription response for video: {VideoUrl}", videoUrl);
            return TranscriptionResult.FailureResult($"Failed to parse transcription response: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transcription failed for video: {VideoUrl}", videoUrl);
            return TranscriptionResult.FailureResult($"Transcription failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts a clean filename from a URL, handling URL encoding.
    /// </summary>
    private static string? GetFileNameFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var fileName = Path.GetFileName(uri.LocalPath);
            // Decode URL-encoded characters (e.g., %20 -> space)
            var decodedFileName = Uri.UnescapeDataString(fileName);
            // Replace spaces with underscores for cleaner handling
            return decodedFileName.Replace(" ", "_");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Determines the MIME content type based on file extension.
    /// </summary>
    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        return extension switch
        {
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mov" => "video/quicktime",
            ".avi" => "video/x-msvideo",
            ".mkv" => "video/x-matroska",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".m4a" => "audio/mp4",
            _ => "video/mp4" // Default to mp4
        };
    }

    /// <summary>
    /// Parses the ElevenLabs API response into a list of transcript words.
    /// </summary>
    private static List<TranscriptWord> ParseTranscriptionResponse(string responseBody)
    {
        var words = new List<TranscriptWord>();

        using var jsonDoc = JsonDocument.Parse(responseBody);

        if (!jsonDoc.RootElement.TryGetProperty("words", out var wordsElement))
            return words;

        foreach (var wordElement in wordsElement.EnumerateArray())
        {
            words.Add(new TranscriptWord
            {
                Text = wordElement.TryGetProperty("text", out var textEl)
                    ? textEl.GetString() ?? string.Empty
                    : string.Empty,
                Type = wordElement.TryGetProperty("type", out var typeEl)
                    ? typeEl.GetString() ?? "word"
                    : "word",
                Start = wordElement.TryGetProperty("start", out var startEl)
                    ? startEl.GetDecimal()
                    : 0,
                End = wordElement.TryGetProperty("end", out var endEl)
                    ? endEl.GetDecimal()
                    : 0
            });
        }

        return words;
    }
}
