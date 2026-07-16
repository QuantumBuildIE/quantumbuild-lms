using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;

namespace QuantumBuild.Tests.Integration.Setup.Fakes;

/// <summary>
/// Fake R2 storage service for testing that stores files in memory.
/// </summary>
public class FakeR2StorageService : IR2StorageService
{
    private readonly Dictionary<string, byte[]> _files = new();

    /// <summary>
    /// Gets the files that have been stored (key → bytes).
    /// </summary>
    public IReadOnlyDictionary<string, byte[]> StoredFiles => _files;

    /// <summary>
    /// When true, UploadCertificateAsync throws instead of succeeding — simulates an infrastructure
    /// failure inside certificate generation so tests can verify the exception-swallow gap is closed.
    /// </summary>
    public bool ShouldThrowOnUploadCertificate { get; set; }

    /// <summary>
    /// Reset to default state and clear stored files.
    /// </summary>
    public void Reset()
    {
        _files.Clear();
        ShouldThrowOnUploadCertificate = false;
    }

    public Task<R2UploadResult> UploadSubtitleAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        string talkTitle,
        string languageCode,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var key = $"{tenantId}/subs/{toolboxTalkId}_{languageCode}.srt";
        var bytes = ReadStream(content);
        _files[key] = bytes;
        return Task.FromResult(R2UploadResult.SuccessResult(
            $"https://fake-r2.test/{key}", key, bytes.Length, "application/x-subrip"));
    }

    public Task<R2UploadResult> UploadVideoAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        string talkTitle,
        Stream content,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(originalFileName);
        var key = $"{tenantId}/videos/{toolboxTalkId}{ext}";
        var bytes = ReadStream(content);
        _files[key] = bytes;
        return Task.FromResult(R2UploadResult.SuccessResult(
            $"https://fake-r2.test/{key}", key, bytes.Length, "video/mp4"));
    }

    public Task<R2UploadResult> UploadPdfAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        string talkTitle,
        Stream content,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        var key = $"{tenantId}/pdfs/{toolboxTalkId}.pdf";
        var bytes = ReadStream(content);
        _files[key] = bytes;
        return Task.FromResult(R2UploadResult.SuccessResult(
            $"https://fake-r2.test/{key}", key, bytes.Length, "application/pdf"));
    }

    public Task<R2UploadResult> UploadCertificateAsync(
        Guid tenantId,
        string certificateNumber,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (ShouldThrowOnUploadCertificate)
            throw new InvalidOperationException("Simulated certificate upload failure.");

        var key = $"{tenantId}/certificates/{certificateNumber}.pdf";
        var bytes = ReadStream(content);
        _files[key] = bytes;
        return Task.FromResult(R2UploadResult.SuccessResult(
            $"https://fake-r2.test/{key}", key, bytes.Length, "application/pdf"));
    }

    public Task<R2UploadResult> UploadValidationReportAsync(
        Guid tenantId,
        Guid validationRunId,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var key = $"{tenantId}/validation-reports/{validationRunId}.pdf";
        var bytes = ReadStream(content);
        _files[key] = bytes;
        return Task.FromResult(R2UploadResult.SuccessResult(
            $"https://fake-r2.test/{key}", key, bytes.Length, "application/pdf"));
    }

    public Task<R2UploadResult> UploadSlideImageAsync(
        string storagePath,
        byte[] imageBytes,
        CancellationToken cancellationToken = default)
    {
        _files[storagePath] = imageBytes;
        return Task.FromResult(R2UploadResult.SuccessResult(
            $"https://fake-r2.test/{storagePath}", storagePath, imageBytes.Length, "image/png"));
    }

    public Task<byte[]?> DownloadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_files.TryGetValue(path, out var bytes) ? bytes : null);
    }

    public Task DeleteToolboxTalkFilesAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        CancellationToken cancellationToken = default)
    {
        var prefix = $"{tenantId}/";
        var idStr = toolboxTalkId.ToString();
        var keysToRemove = _files.Keys.Where(k => k.StartsWith(prefix) && k.Contains(idStr)).ToList();
        foreach (var key in keysToRemove)
            _files.Remove(key);
        return Task.CompletedTask;
    }

    public Task DeleteVideoAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        CancellationToken cancellationToken = default)
    {
        var prefix = $"{tenantId}/videos/{toolboxTalkId}";
        var keysToRemove = _files.Keys.Where(k => k.StartsWith(prefix)).ToList();
        foreach (var key in keysToRemove)
            _files.Remove(key);
        return Task.CompletedTask;
    }

    public Task DeletePdfAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        CancellationToken cancellationToken = default)
    {
        var prefix = $"{tenantId}/pdfs/{toolboxTalkId}";
        var keysToRemove = _files.Keys.Where(k => k.StartsWith(prefix)).ToList();
        foreach (var key in keysToRemove)
            _files.Remove(key);
        return Task.CompletedTask;
    }

    public Task<R2UploadResult> UploadCoverImageAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        Stream content,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(originalFileName).TrimStart('.').ToLowerInvariant();
        if (ext is not "png" and not "jpg" and not "jpeg") ext = "png";
        var key = $"{tenantId}/cover-images/{toolboxTalkId:N}-cover.{ext}";
        var bytes = ReadStream(content);
        _files[key] = bytes;
        return Task.FromResult(R2UploadResult.SuccessResult(
            $"https://fake-r2.test/{key}", key, bytes.Length, "image/png"));
    }

    public Task DeleteCoverImageAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        CancellationToken cancellationToken = default)
    {
        var prefix = $"{tenantId}/cover-images/{toolboxTalkId:N}-cover.";
        var keysToRemove = _files.Keys.Where(k => k.StartsWith(prefix)).ToList();
        foreach (var key in keysToRemove)
            _files.Remove(key);
        return Task.CompletedTask;
    }

    public Task<R2UploadResult> UploadSessionFileAsync(
        Guid tenantId,
        Guid sessionId,
        Stream content,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var key = $"{tenantId}/sessions/{sessionId}/{originalFileName}";
        var bytes = ReadStream(content);
        _files[key] = bytes;
        return Task.FromResult(R2UploadResult.SuccessResult(
            $"https://fake-r2.test/{key}", key, bytes.Length, contentType));
    }

    public Task DeleteAllTenantFilesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var prefix = $"{tenantId}/";
        var keysToRemove = _files.Keys.Where(k => k.StartsWith(prefix)).ToList();
        foreach (var key in keysToRemove)
            _files.Remove(key);
        return Task.CompletedTask;
    }

    public Task DeleteSessionFilesAsync(
        Guid tenantId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var prefix = $"{tenantId}/sessions/{sessionId}/";
        var keysToRemove = _files.Keys.Where(k => k.StartsWith(prefix)).ToList();
        foreach (var key in keysToRemove)
            _files.Remove(key);
        return Task.CompletedTask;
    }

    public Task<R2UploadResult> UploadInspectionReportAsync(
        Guid tenantId,
        string sectorKey,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var key = $"{tenantId}/inspection-reports/{sectorKey}/{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf";
        var bytes = ReadStream(content);
        _files[key] = bytes;
        return Task.FromResult(R2UploadResult.SuccessResult(
            $"https://fake-r2.test/{key}", key, bytes.Length, "application/pdf"));
    }

    public string GeneratePublicUrl(Guid tenantId, string folder, string fileName)
    {
        return $"https://fake-r2.test/{tenantId}/{folder}/{fileName}";
    }

    public Task<R2UploadResult> UploadRegulatoryDocumentAsync(
        Guid regulatoryDocumentId,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var key = $"regulatory/{regulatoryDocumentId}/source.pdf";
        var bytes = ReadStream(content);
        _files[key] = bytes;
        return Task.FromResult(R2UploadResult.SuccessResult(
            $"https://fake-r2.test/{key}", key, bytes.Length, "application/pdf"));
    }

    public Task<R2UploadResult> UploadQrCodeImageAsync(
        Guid tenantId,
        string codeToken,
        byte[] pngBytes,
        CancellationToken cancellationToken = default)
    {
        var key = $"{tenantId}/qr-codes/{codeToken}.png";
        _files[key] = pngBytes;
        return Task.FromResult(R2UploadResult.SuccessResult(
            $"https://fake-r2.test/{key}", key, pngBytes.Length, "image/png"));
    }

    public Task<string> UploadBulkImportCsvAsync(
        Guid tenantId,
        Guid sessionId,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var key = $"{tenantId}/bulk-import/{sessionId}.csv";
        _files[key] = ReadStream(content);
        return Task.FromResult(key);
    }

    public Task DeleteFileAsync(string key, CancellationToken cancellationToken = default)
    {
        _files.Remove(key);
        return Task.CompletedTask;
    }

    public string GetPublicUrl(string key)
    {
        return $"https://fake-r2.test/{key}";
    }

    public Task<string> GenerateUploadUrlAsync(string key, string contentType, TimeSpan expiry)
    {
        return Task.FromResult($"https://fake-r2.test/presigned/{key}");
    }

    private static byte[] ReadStream(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
