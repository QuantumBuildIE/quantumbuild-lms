using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Constants;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Domain;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.API.Controllers;

[ApiController]
[Route("api/tenants/{tenantId:guid}/modules")]
[Authorize(Policy = "Tenant.Manage")]
public class TenantModulesController(ICoreDbContext db, ISystemAuditLogger auditLogger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetModules(Guid tenantId)
    {
        var modules = await db.TenantModules
            .IgnoreQueryFilters()
            .Where(m => m.TenantId == tenantId && !m.IsDeleted)
            .OrderBy(m => m.ModuleName)
            .Select(m => new TenantModuleDto
            {
                ModuleName = m.ModuleName,
                AssignedAt = m.CreatedAt,
                AssignedBy = m.CreatedBy
            })
            .ToListAsync();

        return Ok(modules);
    }

    [HttpPost]
    public async Task<IActionResult> AssignModule(Guid tenantId, [FromBody] AssignModuleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ModuleName))
            return BadRequest(new { error = "ModuleName is required." });

        if (!ModuleNames.All.Contains(request.ModuleName))
            return BadRequest(new { error = $"Unknown module: '{request.ModuleName}'. Valid modules: {string.Join(", ", ModuleNames.All)}" });

        var tenantExists = await db.Tenants
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Id == tenantId && !t.IsDeleted);

        if (!tenantExists)
            return NotFound(new { error = "Tenant not found." });

        var existing = await db.TenantModules
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.ModuleName == request.ModuleName);

        if (existing != null)
        {
            if (!existing.IsDeleted)
                return Conflict(new { error = $"Module '{request.ModuleName}' is already assigned to this tenant." });

            existing.IsDeleted = false;
            await db.SaveChangesAsync();

            await auditLogger.LogAsync(AuditActions.Module.Assign, success: true,
                entityType: "Tenant", entityId: tenantId,
                metadataJson: JsonSerializer.Serialize(new { moduleName = request.ModuleName }));

            return Ok(new TenantModuleDto
            {
                ModuleName = existing.ModuleName,
                AssignedAt = existing.CreatedAt,
                AssignedBy = existing.CreatedBy
            });
        }

        var tenantModule = new TenantModule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ModuleName = request.ModuleName
        };

        db.TenantModules.Add(tenantModule);
        await db.SaveChangesAsync();

        await auditLogger.LogAsync(AuditActions.Module.Assign, success: true,
            entityType: "Tenant", entityId: tenantId,
            metadataJson: JsonSerializer.Serialize(new { moduleName = request.ModuleName }));

        return CreatedAtAction(nameof(GetModules), new { tenantId }, new TenantModuleDto
        {
            ModuleName = tenantModule.ModuleName,
            AssignedAt = tenantModule.CreatedAt,
            AssignedBy = tenantModule.CreatedBy
        });
    }

    [HttpDelete("{moduleName}")]
    public async Task<IActionResult> RemoveModule(Guid tenantId, string moduleName)
    {
        var existing = await db.TenantModules
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.ModuleName == moduleName && !m.IsDeleted);

        if (existing == null)
        {
            await auditLogger.LogAsync(AuditActions.Module.Remove, success: false,
                entityType: "Tenant", entityId: tenantId,
                failureReason: $"Module '{moduleName}' is not assigned to this tenant.",
                metadataJson: JsonSerializer.Serialize(new { moduleName }));
            return NotFound(new { error = $"Module '{moduleName}' is not assigned to this tenant." });
        }

        existing.IsDeleted = true;
        await db.SaveChangesAsync();

        await auditLogger.LogAsync(AuditActions.Module.Remove, success: true,
            entityType: "Tenant", entityId: tenantId,
            metadataJson: JsonSerializer.Serialize(new { moduleName }));
        return NoContent();
    }
}

public record TenantModuleDto
{
    public string ModuleName { get; init; } = string.Empty;
    public DateTime AssignedAt { get; init; }
    public string AssignedBy { get; init; } = string.Empty;
}

public record AssignModuleRequest
{
    public string ModuleName { get; init; } = string.Empty;
}
