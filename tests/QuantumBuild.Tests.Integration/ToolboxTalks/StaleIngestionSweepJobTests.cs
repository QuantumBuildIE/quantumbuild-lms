using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Covers StaleIngestionSweepJob — the backstop for a RegulatoryDocument abandoned mid
/// ingestion (worker process killed before RequirementIngestionJob's own catch block could run
/// again). See docs/ingestion-terminal-state-recon.md §7 and Chunk C.
/// </summary>
[Collection("Integration")]
public class StaleIngestionSweepJobTests : IntegrationTestBase
{
    public StaleIngestionSweepJobTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// Seeds a RegulatoryDocument directly on Add (not Modified), so the explicitly-assigned
    /// UpdatedAt survives ApplicationDbContext.SetAuditFields — that method only overwrites
    /// UpdatedAt for Modified entries, not Added ones (see ApplicationDbContext.cs). This is
    /// what lets a test simulate "has been sitting on Ingesting since an hour ago" without a
    /// second, audit-field-triggering save.
    /// </summary>
    private static async Task<RegulatoryDocument> SeedIngestingDocumentAsync(
        ApplicationDbContext context, DateTime? updatedAt)
    {
        var body = new RegulatoryBody
        {
            Id = Guid.NewGuid(),
            Name = "Sweep Test Body",
            Code = $"STB{Guid.NewGuid():N}"[..10],
            Country = "IE"
        };
        var document = new RegulatoryDocument
        {
            Id = Guid.NewGuid(),
            RegulatoryBodyId = body.Id,
            Title = "Sweep Test Document",
            Version = "1.0",
            SourceUrl = "https://example.test/document.pdf",
            LastIngestionStatus = RegulatoryIngestionStatus.Ingesting,
            UpdatedAt = updatedAt
        };

        context.RegulatoryBodies.Add(body);
        context.RegulatoryDocuments.Add(document);
        await context.SaveChangesAsync();

        return document;
    }

    [Fact]
    public async Task ExecuteAsync_DocumentStuckIngestingPast60Minutes_MarksFailedWithIngestionAbandoned()
    {
        var context = GetDbContext();
        var stuckSince = DateTime.UtcNow.AddMinutes(-90);
        var document = await SeedIngestingDocumentAsync(context, stuckSince);

        var job = new StaleIngestionSweepJob(context, NullLogger<StaleIngestionSweepJob>.Instance);
        await job.ExecuteAsync(CancellationToken.None);

        var reloaded = await context.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);

        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Failed);
        reloaded.LastIngestionErrorCode.Should().Be("ingestion_abandoned");
        reloaded.LastIngestionErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteAsync_DocumentIngestingWithinThreshold_LeavesUntouched()
    {
        // 20 minutes in — well within a legitimate run (worst case ~40 minutes, see recon (c))
        // and well under the sweep's 60-minute threshold. The sweep must not touch it.
        var context = GetDbContext();
        var recentlyStarted = DateTime.UtcNow.AddMinutes(-20);
        var document = await SeedIngestingDocumentAsync(context, recentlyStarted);

        var job = new StaleIngestionSweepJob(context, NullLogger<StaleIngestionSweepJob>.Instance);
        await job.ExecuteAsync(CancellationToken.None);

        var reloaded = await context.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);

        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Ingesting);
        reloaded.LastIngestionErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_DocumentAlreadyTerminal_IsIgnoredRegardlessOfAge()
    {
        // A document that already reached Failed/Success/Skipped long ago must never be
        // reconsidered by the sweep — it only targets rows still stuck on Ingesting.
        var context = GetDbContext();
        var longAgo = DateTime.UtcNow.AddDays(-3);
        var document = await SeedIngestingDocumentAsync(context, longAgo);

        document.LastIngestionStatus = RegulatoryIngestionStatus.Failed;
        document.LastIngestionErrorCode = "fetch_failed";
        document.LastIngestionErrorMessage = "Pre-existing failure, unrelated to the sweep.";
        await context.SaveChangesAsync();

        var job = new StaleIngestionSweepJob(context, NullLogger<StaleIngestionSweepJob>.Instance);
        await job.ExecuteAsync(CancellationToken.None);

        var reloaded = await context.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);

        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Failed);
        reloaded.LastIngestionErrorCode.Should().Be("fetch_failed");
        reloaded.LastIngestionErrorMessage.Should().Be("Pre-existing failure, unrelated to the sweep.");
    }
}
