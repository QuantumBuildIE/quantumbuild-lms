using System.IO.Compression;
using QuantumBuild.Core.Application.Features.BulkSopImport;

namespace QuantumBuild.Core.Infrastructure.Services;

/// <summary>
/// Inspects a ZIP archive of SOP PDFs: classifies entries as queued PDFs or ignored junk,
/// and enforces sane limits against zip-bomb / absurd-archive uploads. Does not extract or
/// upload anything — that happens later in BulkSopImportJob once the user confirms.
/// </summary>
public sealed class BulkSopImportValidationService : IBulkSopImportValidationService
{
    /// <summary>Maximum number of PDF files accepted in a single batch. Generation is AI-driven
    /// and expensive per learning, so this is kept well below the CSV bulk-import row limit.</summary>
    public const int MaxPdfCount = 50;

    /// <summary>Maximum uncompressed size of a single PDF entry — matches the existing
    /// single-PDF upload limit on ToolboxTalkFilesController.UploadPdf.</summary>
    public const long MaxSinglePdfSizeBytes = 50L * 1024 * 1024; // 50 MB

    /// <summary>Maximum combined uncompressed size of all queued PDF entries — the primary
    /// zip-bomb guard, since a crafted archive can report a tiny compressed size.</summary>
    public const long MaxTotalUncompressedBytes = 500L * 1024 * 1024; // 500 MB

    /// <summary>Maximum number of entries examined in the archive at all (including ignored
    /// junk) — guards against archives with an absurd number of tiny entries.</summary>
    public const int MaxZipEntries = 1000;

    public Task<BulkSopImportValidationResult> ValidateAsync(
        Stream zipStream,
        CancellationToken cancellationToken = default)
    {
        ZipArchive archive;
        try
        {
            archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            return Task.FromResult(new BulkSopImportValidationResult
            {
                ArchiveReadable = false,
                ErrorMessage = "The uploaded file is not a valid ZIP archive."
            });
        }

        using (archive)
        {
            if (archive.Entries.Count > MaxZipEntries)
            {
                return Task.FromResult(new BulkSopImportValidationResult
                {
                    ArchiveReadable = false,
                    ErrorMessage = $"Archive contains {archive.Entries.Count} entries; maximum is {MaxZipEntries}."
                });
            }

            var files = new List<BulkSopFileEntry>();
            var ignored = new List<string>();
            long totalUncompressedBytes = 0;

            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!IsCandidatePdf(entry, out var reason))
                {
                    if (reason is not null)
                        ignored.Add($"{entry.FullName} ({reason})");
                    continue;
                }

                if (entry.Length > MaxSinglePdfSizeBytes)
                {
                    return Task.FromResult(new BulkSopImportValidationResult
                    {
                        ArchiveReadable = false,
                        ErrorMessage = $"'{entry.FullName}' is {entry.Length / 1024 / 1024} MB, " +
                            $"which exceeds the {MaxSinglePdfSizeBytes / 1024 / 1024} MB per-file maximum."
                    });
                }

                totalUncompressedBytes += entry.Length;
                if (totalUncompressedBytes > MaxTotalUncompressedBytes)
                {
                    return Task.FromResult(new BulkSopImportValidationResult
                    {
                        ArchiveReadable = false,
                        ErrorMessage = $"Archive contents exceed the " +
                            $"{MaxTotalUncompressedBytes / 1024 / 1024} MB total uncompressed size maximum."
                    });
                }

                files.Add(new BulkSopFileEntry
                {
                    ItemIndex = files.Count,
                    EntryName = entry.FullName,
                    FileName = entry.Name,
                    SizeBytes = entry.Length
                });
            }

            if (files.Count > MaxPdfCount)
            {
                return Task.FromResult(new BulkSopImportValidationResult
                {
                    ArchiveReadable = false,
                    ErrorMessage = $"Archive contains {files.Count} PDF files; maximum per import is {MaxPdfCount}."
                });
            }

            if (files.Count == 0)
            {
                return Task.FromResult(new BulkSopImportValidationResult
                {
                    ArchiveReadable = false,
                    ErrorMessage = "Archive does not contain any PDF files."
                });
            }

            return Task.FromResult(new BulkSopImportValidationResult
            {
                ArchiveReadable = true,
                TotalPdfCount = files.Count,
                TotalUncompressedBytes = totalUncompressedBytes,
                Files = files,
                IgnoredEntryNames = ignored
            });
        }
    }

    /// <summary>
    /// A candidate PDF is a non-directory entry, not a macOS/archiver junk file, whose name
    /// ends in ".pdf". Returns false with a human-readable reason for anything excluded.
    /// </summary>
    private static bool IsCandidatePdf(ZipArchiveEntry entry, out string? reason)
    {
        // Directories show up as zero-length entries with a trailing slash / empty Name.
        if (string.IsNullOrEmpty(entry.Name))
        {
            reason = null; // directory entries are not worth reporting individually
            return false;
        }

        if (entry.FullName.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase)
            || entry.Name.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase)
            || entry.Name.StartsWith("._", StringComparison.Ordinal))
        {
            reason = "macOS archive metadata";
            return false;
        }

        if (!entry.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            reason = "not a PDF file";
            return false;
        }

        if (entry.Length == 0)
        {
            reason = "empty file";
            return false;
        }

        reason = null;
        return true;
    }
}
