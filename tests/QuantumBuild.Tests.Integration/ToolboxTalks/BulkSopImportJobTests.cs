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
        }
    }
}
