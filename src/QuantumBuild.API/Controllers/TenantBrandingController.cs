using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Core.Infrastructure.Data.Configurations;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;

namespace QuantumBuild.API.Controllers;

public record TenantLogoResponse(string? LogoUrl);

/// <summary>
/// Platform-admin management of a tenant's logo. The tenant is taken from the ROUTE and every query
/// uses it explicitly; X-Tenant-Id is never consulted here (TenantBranding has no tenant query filter).
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId:guid}/branding")]
[Authorize(Policy = "Tenant.Manage")]
public class TenantBrandingController(
    ApplicationDbContext db,
    IR2StorageService storage,
    ILogger<TenantBrandingController> logger) : ControllerBase
{
    private const long MaxLogoSizeBytes = 1024 * 1024; // 1MB

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    /// <summary>
    /// Read the logo of the tenant in the route, regardless of the caller's active tenant.
    /// No row or a null key returns a null logo.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(TenantLogoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLogo(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenantError = await ValidateTenantAsync(tenantId, cancellationToken);
        if (tenantError != null)
            return tenantError;

        var logoKey = await db.TenantBrandings
            .Where(b => b.TenantId == tenantId)
            .Select(b => b.LogoKey)
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new TenantLogoResponse(logoKey == null ? null : storage.GetPublicUrl(logoKey)));
    }

    /// <summary>
    /// Upload (or replace) the tenant's logo. PNG or JPEG only, detected from the file's leading bytes;
    /// the client-supplied Content-Type and file name are ignored.
    /// </summary>
    [HttpPut("logo")]
    [RequestSizeLimit(2 * 1024 * 1024)] // multipart overhead headroom; the 1MB file limit is checked below
    [ProducesResponseType(typeof(TenantLogoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadLogo(Guid tenantId, IFormFile? file, CancellationToken cancellationToken)
    {
        var tenantError = await ValidateTenantAsync(tenantId, cancellationToken);
        if (tenantError != null)
            return tenantError;

        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        if (file.Length > MaxLogoSizeBytes)
            return BadRequest(new { error = "Logo must be 1MB or smaller." });

        byte[] bytes;
        await using (var input = file.OpenReadStream())
        using (var ms = new MemoryStream())
        {
            await input.CopyToAsync(ms, cancellationToken);
            bytes = ms.ToArray();
        }

        var detected = DetectImageType(bytes);
        if (detected == null)
            return BadRequest(new { error = "Invalid file type. Only PNG and JPEG images are allowed." });

        var (extension, contentType) = detected.Value;
        var fileName = $"logo-{Guid.NewGuid():N}.{extension}";

        var upload = await storage.UploadTenantLogoAsync(tenantId, fileName, bytes, contentType, cancellationToken);
        if (!upload.Success || upload.Key == null)
            return BadRequest(new { error = upload.ErrorMessage ?? "Logo upload failed." });

        string? previousKey;
        try
        {
            previousKey = await SaveLogoKeyAsync(tenantId, upload.Key, cancellationToken);
        }
        catch
        {
            await TryDeleteObjectAsync(upload.Key, "orphaned new logo after failed save");
            throw;
        }

        if (previousKey != null && previousKey != upload.Key)
            await TryDeleteObjectAsync(previousKey, "previous logo");

        return Ok(new TenantLogoResponse(storage.GetPublicUrl(upload.Key)));
    }

    /// <summary>
    /// Remove the tenant's logo. The branding row is retained with LogoKey nulled. Idempotent.
    /// </summary>
    [HttpDelete("logo")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLogo(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenantError = await ValidateTenantAsync(tenantId, cancellationToken);
        if (tenantError != null)
            return tenantError;

        var branding = await db.TenantBrandings.FirstOrDefaultAsync(b => b.TenantId == tenantId, cancellationToken);
        if (branding?.LogoKey == null)
            return NoContent();

        var key = branding.LogoKey;
        branding.LogoKey = null;
        await db.SaveChangesAsync(cancellationToken);

        await TryDeleteObjectAsync(key, "removed logo");

        return NoContent();
    }

    /// <summary>
    /// Upserts the tenant's branding row and returns the previous LogoKey. Two first uploads can race past
    /// the "no row yet" read; the loser hits the unique TenantId index (23505) and is retried once as an
    /// update of the winner's row (last write wins). Any other failure, or a second failure, propagates.
    /// </summary>
    private async Task<string?> SaveLogoKeyAsync(Guid tenantId, string newKey, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var branding = await db.TenantBrandings.FirstOrDefaultAsync(b => b.TenantId == tenantId, cancellationToken);
            string? previousKey;
            if (branding == null)
            {
                previousKey = null;
                branding = new TenantBranding { Id = Guid.NewGuid(), TenantId = tenantId, LogoKey = newKey };
                db.TenantBrandings.Add(branding);
            }
            else
            {
                previousKey = branding.LogoKey;
                branding.LogoKey = newKey;
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return previousKey;
            }
            catch (DbUpdateException ex) when (attempt == 0 && IsBrandingTenantUniqueViolation(ex))
            {
                logger.LogInformation("Concurrent first logo upload for tenant {TenantId}; retrying as an update", tenantId);
                db.Entry(branding).State = EntityState.Detached;
            }
        }
    }

    private static bool IsBrandingTenantUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: TenantBrandingConfiguration.TenantIdIndexName
        };

    private async Task<IActionResult?> ValidateTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "A tenant id is required." });

        var exists = await db.Tenants.AnyAsync(t => t.Id == tenantId, cancellationToken);
        return exists ? null : NotFound(new { error = "Tenant not found." });
    }

    private static (string Extension, string ContentType)? DetectImageType(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(PngSignature))
            return ("png", "image/png");
        if (bytes.AsSpan().StartsWith(JpegSignature))
            return ("jpg", "image/jpeg");
        return null;
    }

    private async Task TryDeleteObjectAsync(string key, string description)
    {
        try
        {
            await storage.DeleteFileAsync(key);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete {Description} object {Key}; it is now orphaned in R2", description, key);
        }
    }
}
