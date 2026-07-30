using System.IO.Compression;
using System.Net;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Tests.Common.TestTenant;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for Chunk 1 of the SCORM export feature:
///   POST /api/toolbox-talks/{id}/scorm-export
///
/// These are unit-of-work tests confirming the endpoint produces a well-formed ZIP
/// with a valid imsmanifest.xml and index.html. They do NOT validate SCORM conformance
/// against a real LMS — that is SCORM Cloud's job (manual step, see docs/scorm-export-recon.md Part E).
/// </summary>
[Collection("Integration")]
public class ScormExportTests : IntegrationTestBase
{
    public ScormExportTests(CustomWebApplicationFactory factory) : base(factory) { }

    private async Task<Guid> CreateTalkWithSectionAsync(string title = "Working at Heights Safety")
    {
        var talkId = Guid.NewGuid();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.Set<ToolboxTalk>().Add(new ToolboxTalk
        {
            Id = talkId,
            TenantId = TestTenantConstants.TenantId,
            Code = $"SCORM{Guid.NewGuid():N}"[..8],
            Title = title,
            Description = "Test talk for SCORM export integration tests",
            Frequency = ToolboxTalkFrequency.Once,
            VideoSource = VideoSource.None,
            MinimumVideoWatchPercent = 90,
            RequiresQuiz = false,
            IsActive = true,
            GenerateCertificate = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        db.Set<ToolboxTalkSection>().Add(new ToolboxTalkSection
        {
            Id = Guid.NewGuid(),
            ToolboxTalkId = talkId,
            SectionNumber = 1,
            Title = "Section 1",
            Content = "<p>Always use fall protection when working above 2 metres.</p>",
            RequiresAcknowledgment = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();
        return talkId;
    }

    private async Task<Guid> CreateTalkWithoutSectionAsync(string title = "Empty Draft Talk")
    {
        var talkId = Guid.NewGuid();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.Set<ToolboxTalk>().Add(new ToolboxTalk
        {
            Id = talkId,
            TenantId = TestTenantConstants.TenantId,
            Code = $"SCORM{Guid.NewGuid():N}"[..8],
            Title = title,
            Frequency = ToolboxTalkFrequency.Once,
            VideoSource = VideoSource.None,
            MinimumVideoWatchPercent = 90,
            RequiresQuiz = false,
            IsActive = true,
            GenerateCertificate = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();
        return talkId;
    }

    private static (string ManifestXml, string IndexHtml) ExtractZipContents(byte[] zipBytes)
    {
        using var memoryStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

        var manifestEntry = archive.GetEntry("imsmanifest.xml");
        var indexEntry = archive.GetEntry("index.html");

        manifestEntry.Should().NotBeNull("imsmanifest.xml must exist at the ZIP root");
        indexEntry.Should().NotBeNull("index.html must exist at the ZIP root");

        using var manifestReader = new StreamReader(manifestEntry!.Open());
        using var indexReader = new StreamReader(indexEntry!.Open());

        return (manifestReader.ReadToEnd(), indexReader.ReadToEnd());
    }

    // 1 — Happy path: 200, correct content-type, non-empty body
    [Fact]
    public async Task ExportScorm_ExistingTalk_Returns200WithZipContentType()
    {
        var talkId = await CreateTalkWithSectionAsync();

        var response = await AdminClient.PostAsync($"/api/toolbox-talks/{talkId}/scorm-export", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/zip");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty();
    }

    // 2 & 5 — ZIP contains both required root-level entries
    [Fact]
    public async Task ExportScorm_ZipContainsManifestAndIndexHtml()
    {
        var talkId = await CreateTalkWithSectionAsync();

        var response = await AdminClient.PostAsync($"/api/toolbox-talks/{talkId}/scorm-export", null);
        var bytes = await response.Content.ReadAsByteArrayAsync();

        using var memoryStream = new MemoryStream(bytes);
        using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

        archive.Entries.Should().Contain(e => e.FullName == "imsmanifest.xml");
        archive.Entries.Should().Contain(e => e.FullName == "index.html");
    }

    // 3 — imsmanifest.xml parses as valid, well-formed XML
    [Fact]
    public async Task ExportScorm_ManifestParsesAsValidXml()
    {
        var talkId = await CreateTalkWithSectionAsync();

        var response = await AdminClient.PostAsync($"/api/toolbox-talks/{talkId}/scorm-export", null);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var (manifestXml, _) = ExtractZipContents(bytes);

        var act = () => XDocument.Parse(manifestXml);
        act.Should().NotThrow("imsmanifest.xml must be well-formed XML");
    }

    // 4 — imsmanifest.xml contains expected SCORM 1.2 metadata: identifier + talk title
    [Fact]
    public async Task ExportScorm_ManifestContainsExpectedMetadata()
    {
        var title = $"Confined Spaces Entry {Guid.NewGuid():N}"[..40];
        var talkId = await CreateTalkWithSectionAsync(title);

        var response = await AdminClient.PostAsync($"/api/toolbox-talks/{talkId}/scorm-export", null);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var (manifestXml, _) = ExtractZipContents(bytes);

        var doc = XDocument.Parse(manifestXml);
        XNamespace imscp = "http://www.imsproject.org/xsd/imscp_rootv1p1p2";

        var manifestElement = doc.Element(imscp + "manifest");
        manifestElement.Should().NotBeNull();
        manifestElement!.Attribute("identifier")!.Value.Should().Contain(talkId.ToString("N"));

        manifestElement.Descendants(imscp + "schemaversion").First().Value.Should().Be("1.2");

        var organizationTitle = manifestElement
            .Descendants(imscp + "organization").First()
            .Element(imscp + "title")!.Value;
        organizationTitle.Should().Be(title);

        var resource = manifestElement.Descendants(imscp + "resource").First();
        resource.Attribute("href")!.Value.Should().Be("index.html");
        resource.Attribute("type")!.Value.Should().Be("webcontent");
    }

    // 6 — index.html renders the talk's title
    [Fact]
    public async Task ExportScorm_IndexHtmlContainsTalkTitle()
    {
        var title = $"Manual Handling {Guid.NewGuid():N}"[..30];
        var talkId = await CreateTalkWithSectionAsync(title);

        var response = await AdminClient.PostAsync($"/api/toolbox-talks/{talkId}/scorm-export", null);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var (_, indexHtml) = ExtractZipContents(bytes);

        indexHtml.Should().Contain(title);
        indexHtml.Should().Contain("LMSInitialize");
        indexHtml.Should().Contain("LMSCommit");
        indexHtml.Should().Contain("LMSFinish");
    }

    // 7 — Talk with no sections yet still produces a valid package (placeholder content)
    [Fact]
    public async Task ExportScorm_TalkWithNoSections_StillProducesValidPackage()
    {
        var talkId = await CreateTalkWithoutSectionAsync();

        var response = await AdminClient.PostAsync($"/api/toolbox-talks/{talkId}/scorm-export", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var (manifestXml, indexHtml) = ExtractZipContents(bytes);

        var act = () => XDocument.Parse(manifestXml);
        act.Should().NotThrow();
        indexHtml.Should().Contain("No content available");
    }

    // 8 — Non-existent talk → 404
    [Fact]
    public async Task ExportScorm_NonExistentTalk_Returns404()
    {
        var response = await AdminClient.PostAsync($"/api/toolbox-talks/{Guid.NewGuid()}/scorm-export", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 9 — Talk belonging to a different tenant → 404 (tenant isolation)
    [Fact]
    public async Task ExportScorm_TalkFromOtherTenant_Returns404()
    {
        var otherTenantTalkId = Guid.NewGuid();
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Set<ToolboxTalk>().Add(new ToolboxTalk
            {
                Id = otherTenantTalkId,
                TenantId = Guid.NewGuid(),
                Code = $"OTH{Guid.NewGuid():N}"[..8],
                Title = "Other Tenant Talk",
                Frequency = ToolboxTalkFrequency.Once,
                VideoSource = VideoSource.None,
                MinimumVideoWatchPercent = 90,
                RequiresQuiz = false,
                IsActive = true,
                GenerateCertificate = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test"
            });
            await db.SaveChangesAsync();
        }

        var response = await AdminClient.PostAsync($"/api/toolbox-talks/{otherTenantTalkId}/scorm-export", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 10 — Non-Learnings.Admin (Operator has Learnings.View only) → 403
    [Fact]
    public async Task ExportScorm_OperatorWithoutAdminPermission_Returns403()
    {
        var talkId = await CreateTalkWithSectionAsync();

        var response = await OperatorClient.PostAsync($"/api/toolbox-talks/{talkId}/scorm-export", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // 11 — Unauthenticated → 401
    [Fact]
    public async Task ExportScorm_Unauthenticated_Returns401()
    {
        var talkId = await CreateTalkWithSectionAsync();

        var response = await UnauthenticatedClient.PostAsync($"/api/toolbox-talks/{talkId}/scorm-export", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
