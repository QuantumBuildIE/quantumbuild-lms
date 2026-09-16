using System.IO.Compression;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.API.Controllers;
using QuantumBuild.Core.Application.Abstractions;
using QuantumBuild.Core.Application.Features.BulkSopImport;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Core.Domain.Enums;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// End-to-end coverage for BulkSopImportJob against a real ZIP of PDFs, run through the real
/// upload/validate controller action. Hangfire's background server is disabled in tests (see
/// CustomWebApplicationFactory), so the job is invoked directly afterwards — the same pattern
/// MissingTranslationsJobTests uses for its job.
///
/// This is the regression test for the job-scope tenant-context fix: before the fix, every
/// wizard command handler call inside BulkSopImportJob.ProcessItemAsync failed with "Learning
/// not found", because the ambient EF Core tenant query filter
/// (ApplicationDbContext.TenantId, sourced from ICurrentUserService) resolved to Guid.Empty
/// inside the job's per-item DI scope — a Hangfire job has no HttpContext — and matched
/// nothing, even for a ToolboxTalk row the same job had just created with the correct
/// explicit TenantId.
/// </summary>
[Collection("Integration")]
public class BulkSopImportJobTests : IntegrationTestBase
{
    public BulkSopImportJobTests(CustomWebApplicationFactory factory) : base(factory) { }

    private static byte[] BuildZip(params (string FileName, string Content)[] entries)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (fileName, content) in entries)
            {
                var entry = archive.CreateEntry(fileName);
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream);
                writer.Write(content);
            }
        }
        return ms.ToArray();
    }

    private async Task InitialiseExistingDraftTalkAsync(string title)
    {
        var request = new
        {
            Title = title,
            InputMode = "Text",
            SourceLanguageCode = "en",
            SourceText = "Pre-existing learning that collides with one of the ZIP's derived titles.",
            TargetLanguageCodes = Array.Empty<string>(),
            AudienceRole = "Operator",
            PreserveSourceWording = false,
            IncludeQuiz = false,
        };

        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/initialise", request);
        response.EnsureSuccessStatusCode();
    }

    // A real ZIP of three PDFs (well-formed PDF headers, deterministic distinct content) run
    // end to end: upload -> validate -> job -> Draft learnings. One PDF's derived title
    // collides with a learning that already exists, proving a bad item fails in isolation
    // without affecting the other two.
    [Fact]
    public async Task RealZip_EndToEnd_CreatesDraftLearningsAndIsolatesPartialFailure()
    {
        // Arrange — pre-seed a Draft learning whose title collides with the ZIP's
        // "Duplicate_SOP.pdf" entry (DeriveTitle turns underscores into spaces).
        const string duplicateTitle = "Duplicate SOP";
        await InitialiseExistingDraftTalkAsync(duplicateTitle);

        var zipBytes = BuildZip(
            ("Confined Space Entry.pdf", "%PDF-1.4 fake content — Confined Space Entry"),
            ("Duplicate_SOP.pdf", "%PDF-1.4 fake content — Duplicate SOP"),
            ("Working At Height.pdf", "%PDF-1.4 fake content — Working At Height"));

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(zipBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        form.Add(fileContent, "file", "sops.zip");

        // Act 1 — real upload + validation pass (BulkSopImportController.Upload,
        // BulkSopImportValidationService).
        var uploadResponse = await AdminClient.PostAsync("/api/toolbox-talks/bulk-sop-import", form);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var uploadResult = await uploadResponse.Content
            .ReadFromJsonAsync<Result<BulkSopImportUploadResponseDto>>();
        uploadResult.Should().NotBeNull();
        uploadResult!.Success.Should().BeTrue();
        uploadResult.Data!.Validation.TotalPdfCount.Should().Be(3);
        var sessionId = uploadResult.Data.SessionId;

        // Act 2 — run BulkSopImportJob directly. Hangfire's background server does not run in
        // the test host, so BackgroundJob.Enqueue (what Confirm would call) is bypassed in
        // favour of resolving and invoking the job the same way MissingTranslationsJobTests
        // does for MissingTranslationsJob.
        using (var jobScope = Factory.Services.CreateScope())
        {
            var job = jobScope.ServiceProvider.GetRequiredService<IBulkSopImportJob>();
            await job.ExecuteAsync(sessionId, CancellationToken.None);
        }

        // Assert — session reached a terminal Completed status with the expected partial split.
        var statusResponse = await AdminClient.GetAsync($"/api/toolbox-talks/bulk-sop-import/{sessionId}");
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var statusResult = await statusResponse.Content
            .ReadFromJsonAsync<Result<BulkSopImportSessionStatusDto>>();
        statusResult.Should().NotBeNull();
        statusResult!.Data!.Status.Should().Be(nameof(BulkSopImportStatus.Completed));

        var processing = statusResult.Data.Processing;
        processing.Should().NotBeNull();
        processing!.TotalAttempted.Should().Be(3);
        processing.SucceededCount.Should().Be(2);
        processing.FailedCount.Should().Be(1);
        processing.AlreadyExistedCount.Should().Be(0);

        var failedItem = processing.Items.Single(i => i.Status == nameof(BulkSopImportItemStatus.Failed));
        failedItem.FileName.Should().Be("Duplicate_SOP.pdf");
        failedItem.FailureReason.Should().Contain("already exists");

        var succeededItems = processing.Items
            .Where(i => i.Status == nameof(BulkSopImportItemStatus.Succeeded))
            .ToList();
        succeededItems.Should().HaveCount(2);
        succeededItems.Select(i => i.ToolboxTalkTitle).Should().BeEquivalentTo(
            "Confined Space Entry", "Working At Height");

        // Assert — the two succeeded items are real Draft learnings with sections and a quiz
        // (the initialise -> parse -> quiz-generate chain actually ran end to end), which is
        // the previously-unverifiable "Succeeded row renders with the real title and Draft
        // review link" path.
        using var dbScope = Factory.Services.CreateScope();
        var dbContext = dbScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        foreach (var item in succeededItems)
        {
            var talk = await dbContext.Set<ToolboxTalk>()
                .IgnoreQueryFilters()
                .Include(t => t.Sections)
                .Include(t => t.Questions)
                .FirstOrDefaultAsync(t => t.Id == item.ToolboxTalkId
                                       && t.TenantId == TestTenantConstants.TenantId
                                       && !t.IsDeleted);

            talk.Should().NotBeNull($"the learning for '{item.ToolboxTalkTitle}' should exist");
            talk!.Status.Should().Be(ToolboxTalkStatus.Draft);
            talk.Title.Should().Be(item.ToolboxTalkTitle);
            talk.Sections.Count(s => !s.IsDeleted).Should().BeGreaterThan(0,
                "the parse step must have created sections");
            talk.Questions.Should().NotBeEmpty("the quiz-generate step must have created questions");
            talk.BulkTranslationPendingSince.Should().NotBeNull(
                "a successfully-created bulk learning must be flagged for " +
                "BulkLearningTranslationSweepJob to pick up (Bulk SOP Learnings, Chunk 2)");
        }
    }

    // Regression test for Bug A: BulkSopImportJob used to upload each PDF via
    // UploadSessionFileAsync (scratch, {tenantId}/sessions/{sessionId}/...) and never moved it
    // anywhere else, so the end-of-run DeleteSessionFilesAsync cleanup deleted every
    // just-created talk's SourceFileUrl/PdfUrl in the same job run. With the fix, each item is
    // re-uploaded to the permanent talk-ID-keyed pdfs/ location before cleanup runs. This test
    // uses a 3-item ZIP (no collisions) and asserts, after the full run (including cleanup):
    // all three talks exist with distinct ids/codes/titles, and each one's SourceFileUrl/PdfUrl
    // key is still present in storage and is NOT under the wiped sessions/ prefix.
    [Fact]
    public async Task RealZip_MultiItem_PdfsSurviveSessionCleanupAndTalksAreDistinct()
    {
        var zipBytes = BuildZip(
            ("Fire Extinguisher Use.pdf", "%PDF-1.4 fake content — Fire Extinguisher Use"),
            ("Forklift Operation.pdf", "%PDF-1.4 fake content — Forklift Operation"),
            ("Chemical Spill Response.pdf", "%PDF-1.4 fake content — Chemical Spill Response"));

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(zipBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        form.Add(fileContent, "file", "sops.zip");

        var uploadResponse = await AdminClient.PostAsync("/api/toolbox-talks/bulk-sop-import", form);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var uploadResult = await uploadResponse.Content
            .ReadFromJsonAsync<Result<BulkSopImportUploadResponseDto>>();
        var sessionId = uploadResult!.Data!.SessionId;

        using (var jobScope = Factory.Services.CreateScope())
        {
            var job = jobScope.ServiceProvider.GetRequiredService<IBulkSopImportJob>();
            await job.ExecuteAsync(sessionId, CancellationToken.None);
        }

        var statusResponse = await AdminClient.GetAsync($"/api/toolbox-talks/bulk-sop-import/{sessionId}");
        var statusResult = await statusResponse.Content
            .ReadFromJsonAsync<Result<BulkSopImportSessionStatusDto>>();
        statusResult!.Data!.Status.Should().Be(nameof(BulkSopImportStatus.Completed));

        var processing = statusResult.Data.Processing!;
        processing.SucceededCount.Should().Be(3);
        processing.FailedCount.Should().Be(0);

        var succeededItems = processing.Items
            .Where(i => i.Status == nameof(BulkSopImportItemStatus.Succeeded))
            .ToList();
        succeededItems.Should().HaveCount(3);

        // Distinct ids, codes, and titles across all three items — no "first vs. rest" divergence.
        succeededItems.Select(i => i.ToolboxTalkId).Should().OnlyHaveUniqueItems();
        succeededItems.Select(i => i.ToolboxTalkTitle).Should().OnlyHaveUniqueItems();

        using var dbScope = Factory.Services.CreateScope();
        var dbContext = dbScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var talks = new List<ToolboxTalk>();
        foreach (var item in succeededItems)
        {
            var talk = await dbContext.Set<ToolboxTalk>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == item.ToolboxTalkId
                                       && t.TenantId == TestTenantConstants.TenantId
                                       && !t.IsDeleted);
            talk.Should().NotBeNull();
            talks.Add(talk!);
        }

        talks.Select(t => t.Code).Should().OnlyHaveUniqueItems("each talk must get its own generated Code");

        // The session-level cleanup (DeleteSessionFilesAsync) has already run by this point —
        // the job only reaches Completed after that cleanup call. Bug A's signature was every
        // succeeded item's SourceFileUrl/PdfUrl pointing into the now-deleted
        // {tenantId}/sessions/{sessionId}/ prefix.
        var sessionsPrefix = $"{TestTenantConstants.TenantId}/sessions/{sessionId}/";
        foreach (var talk in talks)
        {
            talk.SourceFileUrl.Should().NotBeNullOrEmpty();
            talk.PdfUrl.Should().NotBeNullOrEmpty();
            talk.SourceFileUrl.Should().NotContain(sessionsPrefix,
                $"'{talk.Title}' SourceFileUrl must not point into the wiped scratch prefix");
            talk.PdfUrl.Should().NotContain(sessionsPrefix,
                $"'{talk.Title}' PdfUrl must not point into the wiped scratch prefix");

            var pdfKey = $"{TestTenantConstants.TenantId}/pdfs/{talk.Id}.pdf";
            FakeR2StorageService.StoredFiles.Should().ContainKey(pdfKey,
                $"'{talk.Title}' PDF should have been re-uploaded to the permanent pdfs/ location and survived cleanup");
        }
    }

    // Regression test for the title-collision defect: DeriveTitle used to derive titles from the
    // ZIP entry's basename only, so two PDFs sharing a basename in different in-archive
    // subfolders (a realistic multi-site SOP export) collided on the title-uniqueness check and
    // the second was recorded Failed. With the fix, the in-archive folder path disambiguates
    // same-basename entries.
    [Fact]
    public async Task RealZip_NestedSubfoldersWithSameBasename_ProduceDistinctNonCollidingTalks()
    {
        var zipBytes = BuildZip(
            ("SiteA/Manual Handling.pdf", "%PDF-1.4 fake content — Site A Manual Handling"),
            ("SiteB/Manual Handling.pdf", "%PDF-1.4 fake content — Site B Manual Handling"));

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(zipBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        form.Add(fileContent, "file", "sops.zip");

        var uploadResponse = await AdminClient.PostAsync("/api/toolbox-talks/bulk-sop-import", form);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var uploadResult = await uploadResponse.Content
            .ReadFromJsonAsync<Result<BulkSopImportUploadResponseDto>>();
        var sessionId = uploadResult!.Data!.SessionId;

        using (var jobScope = Factory.Services.CreateScope())
        {
            var job = jobScope.ServiceProvider.GetRequiredService<IBulkSopImportJob>();
            await job.ExecuteAsync(sessionId, CancellationToken.None);
        }

        var statusResponse = await AdminClient.GetAsync($"/api/toolbox-talks/bulk-sop-import/{sessionId}");
        var statusResult = await statusResponse.Content
            .ReadFromJsonAsync<Result<BulkSopImportSessionStatusDto>>();
        statusResult!.Data!.Status.Should().Be(nameof(BulkSopImportStatus.Completed));

        var processing = statusResult.Data.Processing!;
        processing.FailedCount.Should().Be(0,
            "same-basename PDFs in different ZIP subfolders must not collide on title uniqueness");
        processing.SucceededCount.Should().Be(2);

        var titles = processing.Items
            .Where(i => i.Status == nameof(BulkSopImportItemStatus.Succeeded))
            .Select(i => i.ToolboxTalkTitle)
            .ToList();

        titles.Should().OnlyHaveUniqueItems();
        titles.Should().BeEquivalentTo("SiteA - Manual Handling", "SiteB - Manual Handling");
    }

    // Regression test for the "incomplete lesson reported as success" bug: a quiz-generation
    // failure used to be downgraded to a Warning while the item was still reported Succeeded,
    // so an admin saw a normal success row for a learning that actually had no quiz (and, per
    // the companion ContentParserService fix, could also have had zero sections). The item must
    // now be reported Failed like any other per-item failure — see
    // docs/bulk-import-dedup-recon.md §B.3.
    [Fact]
    public async Task RealZip_QuizGenerationFails_ItemReportedFailedNotSucceeded()
    {
        var zipBytes = BuildZip(
            ("Hot Work Permit.pdf", "%PDF-1.4 fake content — Hot Work Permit"));

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(zipBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        form.Add(fileContent, "file", "sops.zip");

        var uploadResponse = await AdminClient.PostAsync("/api/toolbox-talks/bulk-sop-import", form);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var uploadResult = await uploadResponse.Content
            .ReadFromJsonAsync<Result<BulkSopImportUploadResponseDto>>();
        var sessionId = uploadResult!.Data!.SessionId;

        Factory.FakeAiQuizGenerationService.ShouldFail = true;
        try
        {
            using (var jobScope = Factory.Services.CreateScope())
            {
                var job = jobScope.ServiceProvider.GetRequiredService<IBulkSopImportJob>();
                await job.ExecuteAsync(sessionId, CancellationToken.None);
            }
        }
        finally
        {
            Factory.FakeAiQuizGenerationService.ShouldFail = false;
        }

        var statusResponse = await AdminClient.GetAsync($"/api/toolbox-talks/bulk-sop-import/{sessionId}");
        var statusResult = await statusResponse.Content
            .ReadFromJsonAsync<Result<BulkSopImportSessionStatusDto>>();
        statusResult!.Data!.Status.Should().Be(nameof(BulkSopImportStatus.Completed));

        var processing = statusResult.Data.Processing!;
        processing.TotalAttempted.Should().Be(1);
        processing.SucceededCount.Should().Be(0,
            "a quiz-generation failure must not be reported as a success");
        processing.FailedCount.Should().Be(1);

        var failedItem = processing.Items.Single();
        failedItem.Status.Should().Be(nameof(BulkSopImportItemStatus.Failed));
        failedItem.FailureReason.Should().Contain("Quiz generation failed");
        failedItem.Warning.Should().BeNull("the Warning-and-Succeed downgrade path no longer exists");
    }

    // Companion regression test: a parse failure (which is what the real
    // ContentParserService now returns for a zero-section AI response — see the
    // ContentParserService unit tests) must also be reported as Failed, not Succeeded.
    // BulkSopImportJob.cs already forwarded parseResult failures to Failed() before this
    // fix — this locks in that existing wiring stays correct now that a zero-section parse
    // is one of the ways a real failure can arrive here.
    [Fact]
    public async Task RealZip_ParsingFails_ItemReportedFailedNotSucceeded()
    {
        var zipBytes = BuildZip(
            ("Lockout Tagout.pdf", "%PDF-1.4 fake content — Lockout Tagout"));

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(zipBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        form.Add(fileContent, "file", "sops.zip");

        var uploadResponse = await AdminClient.PostAsync("/api/toolbox-talks/bulk-sop-import", form);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var uploadResult = await uploadResponse.Content
            .ReadFromJsonAsync<Result<BulkSopImportUploadResponseDto>>();
        var sessionId = uploadResult!.Data!.SessionId;

        Factory.FakeContentParserService.ShouldFail = true;
        try
        {
            using (var jobScope = Factory.Services.CreateScope())
            {
                var job = jobScope.ServiceProvider.GetRequiredService<IBulkSopImportJob>();
                await job.ExecuteAsync(sessionId, CancellationToken.None);
            }
        }
        finally
        {
            Factory.FakeContentParserService.ShouldFail = false;
        }

        var statusResponse = await AdminClient.GetAsync($"/api/toolbox-talks/bulk-sop-import/{sessionId}");
        var statusResult = await statusResponse.Content
            .ReadFromJsonAsync<Result<BulkSopImportSessionStatusDto>>();
        statusResult!.Data!.Status.Should().Be(nameof(BulkSopImportStatus.Completed));

        var processing = statusResult.Data.Processing!;
        processing.SucceededCount.Should().Be(0, "a parse failure must not be reported as a success");
        processing.FailedCount.Should().Be(1);

        var failedItem = processing.Items.Single();
        failedItem.Status.Should().Be(nameof(BulkSopImportItemStatus.Failed));
        failedItem.FailureReason.Should().NotBeNullOrWhiteSpace();
    }
}
