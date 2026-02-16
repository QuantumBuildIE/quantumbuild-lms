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
    /// Reset to default state and clear stored files.
    /// </summary>
    public void Reset()
    {
        _files.Clear();
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
        var key = $"{tenantId}/certificates/{certificateNumber}.pdf";
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

    public string GeneratePublicUrl(Guid tenantId, string folder, string fileName)
    {
        return $"https://fake-r2.test/{tenantId}/{folder}/{fileName}";
    }

    private static byte[] ReadStream(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
