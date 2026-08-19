using System.Net;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Tests.Common.TestTenant;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Characterisation test for the EF tracked-parent nav-collection Add() defect in
/// <c>AuditCorpusService.AddEntryAsync</c>, fixed alongside the schedule-assignment
/// instance of the same trap (commit 5104b20). See docs/ef-tracked-parent-add-pattern-recon.md.
///
/// Adding an entry to an already-persisted, re-loaded-and-tracked <see cref="AuditCorpus"/>
/// via <c>corpus.Entries.Add(entry)</c> without an explicit <c>EntityState.Added</c> caused EF
/// to misclassify the new, client-key-assigned entry as Modified and issue a 0-row UPDATE,
/// throwing DbUpdateConcurrencyException. This path had zero prior test coverage.
/// </summary>
public class AuditCorpusEntryTests : IntegrationTestBase
{
    public AuditCorpusEntryTests(CustomWebApplicationFactory factory) : base(factory) { }

    private record AuditCorpusEntryDto(Guid Id, string EntryRef, bool IsActive);
    private record AuditCorpusDto(Guid Id, int Version, int EntryCount, int ActiveEntryCount);

    /// <summary>
    /// Seeds a corpus with one pre-existing entry directly in the DB (simulating a corpus
    /// already frozen from a talk), then reloads it exactly as AddEntryAsync does
    /// (FirstOrDefaultAsync + Include(c => c.Entries)) before adding a second, manual entry
    /// through the real controller/service path. This is the exact "add to an existing,
    /// already-tracked corpus" scenario the recon identified as at risk.
    /// </summary>
    [Fact]
    public async Task AddEntry_ToExistingCorpusWithOneEntry_InsertsSecondEntry_WithoutConcurrencyException()
    {
        // Arrange — seed a corpus with a single existing entry (the "frozen" entry)
        var dbContext = GetService<IToolboxTalksDbContext>();

        var corpus = new AuditCorpus
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantConstants.TenantId,
            CorpusId = $"CORPUS-{Guid.NewGuid():N}"[..12],
            Name = "Existing Corpus",
            SectorKey = "general",
            LanguagePair = "en-fr",
            IsLocked = false,
            Version = 1,
            CreatedBy = "seed",
            CreatedAt = DateTime.UtcNow,
        };

        var firstEntry = new AuditCorpusEntry
        {
            Id = Guid.NewGuid(),
            CorpusId = corpus.Id,
            EntryRef = $"{corpus.CorpusId}-E01",
            SectionTitle = "Section One",
            OriginalText = "Original text one",
            TranslatedText = "Texte original un",
            SourceLanguage = "en",
            TargetLanguage = "fr",
            SectorKey = "general",
            PassThreshold = 75,
            ExpectedOutcome = ValidationOutcome.Pass,
            IsActive = true,
            CreatedBy = "seed",
            CreatedAt = DateTime.UtcNow,
        };

        dbContext.AuditCorpora.Add(corpus);
        dbContext.AuditCorpusEntries.Add(firstEntry);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var addEntryRequest = new
        {
            SectionTitle = "Section Two",
            OriginalText = "Original text two",
            TranslatedText = "Texte original deux",
            SourceLanguage = "en",
            TargetLanguage = "fr",
            PassThreshold = 75,
            ExpectedOutcome = ValidationOutcome.Pass,
            IsSafetyCritical = false,
        };

        // Act — add a second entry to the already-persisted, now re-loaded/tracked corpus
        var response = await AdminClient.PostAsJsonAsync(
            $"/api/toolbox-talks/pipeline/corpus/{corpus.Id}/entries", addEntryRequest);

        // Assert — succeeds with an INSERT, not a DbUpdateConcurrencyException (which the
        // controller's generic catch would surface as a 500)
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<AuditCorpusDto>();
        dto!.EntryCount.Should().Be(2);
        dto.ActiveEntryCount.Should().Be(2);

        // First entry undisturbed
        var persistedFirstEntry = await dbContext.AuditCorpusEntries
            .FirstAsync(e => e.Id == firstEntry.Id);
        persistedFirstEntry.SectionTitle.Should().Be("Section One");
        persistedFirstEntry.IsActive.Should().BeTrue();

        // Second entry actually inserted
        var persistedSecondEntry = await dbContext.AuditCorpusEntries
            .Where(e => e.CorpusId == corpus.Id && e.Id != firstEntry.Id)
            .ToListAsync();
        persistedSecondEntry.Should().HaveCount(1);
        persistedSecondEntry[0].SectionTitle.Should().Be("Section Two");
        persistedSecondEntry[0].TranslatedText.Should().Be("Texte original deux");
    }
}
