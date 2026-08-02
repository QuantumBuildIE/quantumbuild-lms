namespace QuantumBuild.Core.Application.Features.BulkSopImport;

/// <summary>
/// Top-level result returned by IBulkSopImportValidationService.ValidateAsync.
/// Serialised to BulkSopImportSession.ValidationResultJson; deserialised by the Hangfire job.
/// </summary>
public sealed record BulkSopImportValidationResult
{
    /// <summary>True only for a structural failure (corrupt/unreadable archive). When false,
    /// ErrorMessage explains why and no other field is meaningful.</summary>
    public bool ArchiveReadable { get; init; } = true;

    public string? ErrorMessage { get; init; }

    /// <summary>Count of ZIP entries recognised as PDFs and queued for processing.</summary>
    public int TotalPdfCount { get; init; }

    /// <summary>Total uncompressed size, in bytes, of the queued PDF entries.</summary>
    public long TotalUncompressedBytes { get; init; }

    /// <summary>The PDF entries to process, in ZIP order.</summary>
    public List<BulkSopFileEntry> Files { get; init; } = new();

    /// <summary>Human-readable names of ZIP entries that were excluded (non-PDF, junk, directories).</summary>
    public List<string> IgnoredEntryNames { get; init; } = new();
}

/// <summary>One PDF entry queued for learning creation.</summary>
public sealed record BulkSopFileEntry
{
    /// <summary>0-based position among the queued PDF entries — used to derive a stable,
    /// collision-free R2 file name for the re-uploaded copy.</summary>
    public int ItemIndex { get; init; }

    /// <summary>Full path of the entry inside the ZIP archive — used to re-locate the entry
    /// via ZipArchive.GetEntry when the Hangfire job re-opens the archive.</summary>
    public string EntryName { get; init; } = string.Empty;

    /// <summary>Leaf file name (no directories), used to derive the learning title.</summary>
    public string FileName { get; init; } = string.Empty;

    public long SizeBytes { get; init; }
}
