using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Covers the regulatory ingestion file-upload sub-chunk: POST
/// /api/regulatory/documents/{id}/upload stores a source PDF in R2 (faked in tests via
/// FakeR2StorageService) and updates RegulatoryDocument.SourceUrl. Ingestion itself is NOT
/// triggered by upload — that remains a separate explicit action, so these tests only assert
/// on the upload endpoint's own behaviour.
///
/// RegulatoryDocument is a system-managed entity with no TenantId (see RegulatoryDocument.cs),
/// so there is no tenant-scoping dimension to test here — a "wrong tenant" 404 case does not
/// apply. The non-existent-document case below is the closest equivalent.
/// </summary>
[Collection("Integration")]
public class RegulatoryDocumentUploadTests : IntegrationTestBase
{
    public RegulatoryDocumentUploadTests(CustomWebApplicationFactory factory) : base(factory) { }

    private static async Task<RegulatoryDocument> CreateDocumentAsync(
        ApplicationDbContext context, string? sourceUrl = null)
    {
        var body = new RegulatoryBody
        {
            Id = Guid.NewGuid(),
            Name = "Upload Test Body",
            Code = $"UTB{Guid.NewGuid():N}"[..10],
            Country = "IE"
        };
        var document = new RegulatoryDocument
        {
            Id = Guid.NewGuid(),
            RegulatoryBodyId = body.Id,
            Title = "Upload Test Document",
            Version = "1.0",
            SourceUrl = sourceUrl
        };

        context.RegulatoryBodies.Add(body);
        context.RegulatoryDocuments.Add(document);
        await context.SaveChangesAsync();

        return document;
    }

    private static MultipartFormDataContent BuildPdfContent(byte[] bytes, string fileName = "document.pdf")
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", fileName);
        return form;
    }

    [Fact]
    public async Task UploadSourceDocument_ValidPdf_UpdatesSourceUrl()
    {
        var context = GetDbContext();
        var document = await CreateDocumentAsync(context);

        using var content = BuildPdfContent("%PDF-1.4 fake content"u8.ToArray());
        var response = await AdminClient.PostAsync(
            $"/api/regulatory/documents/{document.Id}/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<RegulatoryDocumentUploadResponseDto>();
        dto.Should().NotBeNull();
        dto!.SourceUrl.Should().NotBeNullOrEmpty();
        dto.FileName.Should().Be("document.pdf");
        dto.FileSizeBytes.Should().BeGreaterThan(0);

        var freshContext = GetDbContext();
        var reloaded = await freshContext.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);
        reloaded.SourceUrl.Should().Be(dto.SourceUrl);
    }

    [Fact]
    public async Task UploadSourceDocument_NonPdf_Returns400()
    {
        var context = GetDbContext();
        var document = await CreateDocumentAsync(context);

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("not a pdf"u8.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(fileContent, "file", "document.txt");

        var response = await AdminClient.PostAsync(
            $"/api/regulatory/documents/{document.Id}/upload", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var freshContext = GetDbContext();
        var reloaded = await freshContext.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);
        reloaded.SourceUrl.Should().BeNull();
    }

    [Fact]
    public async Task UploadSourceDocument_EmptyFile_Returns400()
    {
        var context = GetDbContext();
        var document = await CreateDocumentAsync(context);

        using var content = BuildPdfContent([]);
        var response = await AdminClient.PostAsync(
            $"/api/regulatory/documents/{document.Id}/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadSourceDocument_OversizedFile_Returns400()
    {
        var context = GetDbContext();
        var document = await CreateDocumentAsync(context);

        // One byte over the 50MB limit enforced both by [RequestSizeLimit] and the
        // controller's explicit length check.
        var oversized = new byte[52428800 + 1];
        using var content = BuildPdfContent(oversized);

        var response = await AdminClient.PostAsync(
            $"/api/regulatory/documents/{document.Id}/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadSourceDocument_NonExistentDocument_Returns404()
    {
        using var content = BuildPdfContent("%PDF-1.4 fake content"u8.ToArray());
        var response = await AdminClient.PostAsync(
            $"/api/regulatory/documents/{Guid.NewGuid()}/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UploadSourceDocument_DoesNotTriggerIngestion()
    {
        var context = GetDbContext();
        var document = await CreateDocumentAsync(context);

        using var content = BuildPdfContent("%PDF-1.4 fake content"u8.ToArray());
        var response = await AdminClient.PostAsync(
            $"/api/regulatory/documents/{document.Id}/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var freshContext = GetDbContext();
        var reloaded = await freshContext.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);
        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Idle);
        reloaded.LastIngestedAt.Should().BeNull();
    }

    [Fact]
    public async Task UploadSourceDocument_SecondUpload_OverwritesPreviousSourceUrl()
    {
        var context = GetDbContext();
        var document = await CreateDocumentAsync(context, "https://example.com/old-document.pdf");

        using var content = BuildPdfContent("%PDF-1.4 new content"u8.ToArray());
        var response = await AdminClient.PostAsync(
            $"/api/regulatory/documents/{document.Id}/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<RegulatoryDocumentUploadResponseDto>();
        dto!.SourceUrl.Should().NotBe("https://example.com/old-document.pdf");

        var freshContext = GetDbContext();
        var reloaded = await freshContext.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);
        reloaded.SourceUrl.Should().Be(dto.SourceUrl);
    }
}
