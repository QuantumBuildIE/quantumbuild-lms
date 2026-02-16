namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;

/// <summary>
/// Service for managing Toolbox Talk media files in R2 storage with tenant isolation.
/// All files are stored under {tenant-id}/ prefix.
/// </summary>
public interface IR2StorageService
{
    /// <summary>
    /// Uploads a subtitle file to R2 storage.
    /// Path: {tenantId}/subs/{title-slug}_{id}_{languageCode}.srt
    /// </summary>
    Task<R2UploadResult> UploadSubtitleAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        string talkTitle,
        string languageCode,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a video file to R2 storage.
    /// Path: {tenantId}/videos/{title-slug}_{id}.{ext}
    /// </summary>
    Task<R2UploadResult> UploadVideoAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        string talkTitle,
        Stream content,
        string originalFileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a PDF file to R2 storage.
    /// Path: {tenantId}/pdfs/{title-slug}_{id}.pdf
    /// </summary>
    Task<R2UploadResult> UploadPdfAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        string talkTitle,
        Stream content,
        string originalFileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a certificate PDF to R2 storage.
    /// Path: {tenantId}/certificates/{certificateNumber}.pdf
    /// </summary>
    Task<R2UploadResult> UploadCertificateAsync(
        Guid tenantId,
        string certificateNumber,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a slide image (PNG) to R2 storage at the given storage path.
    /// Path: {tenantId}/slides/{toolboxTalkId}/{pageNumber}.png
    /// </summary>
    Task<R2UploadResult> UploadSlideImageAsync(
        string storagePath,
        byte[] imageBytes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file from R2 storage by its storage key/path.
    /// Returns null if the file is not found.
    /// </summary>
    Task<byte[]?> DownloadFileAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all files associated with a Toolbox Talk (videos, PDFs, subtitles).
    /// </summary>
    Task DeleteToolboxTalkFilesAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the video file for a specific Toolbox Talk.
    /// </summary>
    Task DeleteVideoAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the PDF file for a specific Toolbox Talk.
    /// </summary>
    Task DeletePdfAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates the public URL for a file in the R2 bucket.
    /// </summary>
    string GeneratePublicUrl(Guid tenantId, string folder, string fileName);
}
