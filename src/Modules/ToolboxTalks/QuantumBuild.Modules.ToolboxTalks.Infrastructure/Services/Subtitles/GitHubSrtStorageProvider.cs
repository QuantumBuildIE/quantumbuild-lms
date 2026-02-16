using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Subtitles;

/// <summary>
/// SRT storage provider using GitHub repository.
/// Uploads SRT files to a GitHub repository for public access via raw.githubusercontent.com.
/// </summary>
public class GitHubSrtStorageProvider : ISrtStorageProvider
{
    private readonly HttpClient _httpClient;
    private readonly SubtitleProcessingSettings _settings;
    private readonly ILogger<GitHubSrtStorageProvider> _logger;

    public GitHubSrtStorageProvider(
        HttpClient httpClient,
        IOptions<SubtitleProcessingSettings> settings,
        ILogger<GitHubSrtStorageProvider> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Uploads an SRT file to the configured GitHub repository.
    /// Files are stored under {tenantId}/{path}/{fileName} for tenant isolation.
    /// </summary>
    public async Task<SrtUploadResult> UploadSrtAsync(
        string srtContent,
        string fileName,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var github = _settings.SrtStorage.GitHub;
            // Build path with tenant isolation: {tenantId}/{path}/{fileName}
            var path = string.IsNullOrEmpty(github.Path)
                ? $"{tenantId}/{fileName}"
                : $"{tenantId}/{github.Path}/{fileName}";

            var apiUrl = $"https://api.github.com/repos/{github.Owner}/{github.Repo}/contents/{path}";

            _logger.LogInformation("Uploading SRT to GitHub: {Path}", path);

            // Check if file exists (to get SHA for update)
            var existingSha = await GetExistingFileShaAsync(apiUrl, cancellationToken);

            // Upload/Update file
            var contentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(srtContent));

            var requestBody = new Dictionary<string, object>
            {
                { "message", $"Add/Update subtitle: {fileName}" },
                { "content", contentBase64 },
                { "branch", github.Branch }
            };

            if (!string.IsNullOrEmpty(existingSha))
            {
                requestBody["sha"] = existingSha;
                _logger.LogInformation("File exists, will update. SHA: {Sha}", existingSha);
            }

            var uploadRequest = new HttpRequestMessage(HttpMethod.Put, apiUrl);
            AddGitHubHeaders(uploadRequest);
            uploadRequest.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var uploadResponse = await _httpClient.SendAsync(uploadRequest, cancellationToken);
            var uploadBody = await uploadResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!uploadResponse.IsSuccessStatusCode)
            {
                _logger.LogError("GitHub upload failed: {StatusCode} - {Response}",
                    uploadResponse.StatusCode, uploadBody);

                return SrtUploadResult.FailureResult($"GitHub upload failed: {uploadResponse.StatusCode}");
            }

            // Build raw content URL
            var rawUrl = $"https://raw.githubusercontent.com/{github.Owner}/{github.Repo}/{github.Branch}/{path}";

            _logger.LogInformation("Successfully uploaded SRT to GitHub: {Url}", rawUrl);

            return SrtUploadResult.SuccessResult(rawUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload SRT to GitHub: {FileName}", fileName);
            return SrtUploadResult.FailureResult($"Upload failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves SRT content from the GitHub repository.
    /// </summary>
    public async Task<string?> GetSrtContentAsync(string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            var github = _settings.SrtStorage.GitHub;
            var path = string.IsNullOrEmpty(github.Path)
                ? fileName
                : $"{github.Path}/{fileName}";

            var rawUrl = $"https://raw.githubusercontent.com/{github.Owner}/{github.Repo}/{github.Branch}/{path}";

            var response = await _httpClient.GetAsync(rawUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get SRT from GitHub: {FileName}", fileName);
            return null;
        }
    }

    /// <summary>
    /// Deletes an SRT file from the GitHub repository.
    /// </summary>
    public async Task<bool> DeleteSrtAsync(string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            var github = _settings.SrtStorage.GitHub;
            var path = string.IsNullOrEmpty(github.Path)
                ? fileName
                : $"{github.Path}/{fileName}";

            var apiUrl = $"https://api.github.com/repos/{github.Owner}/{github.Repo}/contents/{path}";

            // Get SHA first
            var sha = await GetExistingFileShaAsync(apiUrl, cancellationToken);

            if (string.IsNullOrEmpty(sha))
            {
                _logger.LogWarning("File not found for deletion: {FileName}", fileName);
                return false;
            }

            // Delete file
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, apiUrl);
            AddGitHubHeaders(deleteRequest);
            deleteRequest.Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    message = $"Delete subtitle: {fileName}",
                    sha,
                    branch = github.Branch
                }),
                Encoding.UTF8,
                "application/json");

            var deleteResponse = await _httpClient.SendAsync(deleteRequest, cancellationToken);

            if (deleteResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully deleted SRT from GitHub: {FileName}", fileName);
                return true;
            }

            _logger.LogWarning("Failed to delete SRT from GitHub: {FileName}, Status: {Status}",
                fileName, deleteResponse.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete SRT from GitHub: {FileName}", fileName);
            return false;
        }
    }

    /// <summary>
    /// Gets the SHA of an existing file in the repository.
    /// Returns null if the file doesn't exist.
    /// </summary>
    private async Task<string?> GetExistingFileShaAsync(string apiUrl, CancellationToken cancellationToken)
    {
        try
        {
            var checkRequest = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            AddGitHubHeaders(checkRequest);

            var checkResponse = await _httpClient.SendAsync(checkRequest, cancellationToken);

            if (!checkResponse.IsSuccessStatusCode)
                return null;

            var checkBody = await checkResponse.Content.ReadAsStringAsync(cancellationToken);
            using var checkJson = JsonDocument.Parse(checkBody);

            return checkJson.RootElement.TryGetProperty("sha", out var shaEl)
                ? shaEl.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Adds required GitHub API headers to the request.
    /// </summary>
    private void AddGitHubHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("Authorization", $"Bearer {_settings.SrtStorage.GitHub.Token}");
        request.Headers.Add("Accept", "application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        request.Headers.Add("User-Agent", "QuantumBuild-ToolboxTalks");
    }
}
