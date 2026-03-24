using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.API.Controllers;

/// <summary>
/// Controller for managing safety glossary sectors and terms
/// </summary>
[ApiController]
[Route("api/toolbox-talks/glossary")]
[Authorize(Policy = "Learnings.View")]
public class SafetyGlossaryController : ControllerBase
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SafetyGlossaryController> _logger;

    public SafetyGlossaryController(
        IToolboxTalksDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<SafetyGlossaryController> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// List all sectors visible to this tenant (system defaults + tenant overrides) with term counts
    /// </summary>
    [HttpGet("sectors")]
    [ProducesResponseType(typeof(List<GlossarySectorListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSectors(CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            // System defaults (TenantId == null) + tenant-specific glossaries
            var sectors = await _dbContext.SafetyGlossaries
                .Where(g => g.TenantId == null || g.TenantId == tenantId)
                .Where(g => g.IsActive)
                .Select(g => new GlossarySectorListDto
                {
                    Id = g.Id,
                    SectorKey = g.SectorKey,
                    SectorName = g.SectorName,
                    SectorIcon = g.SectorIcon,
                    IsSystemDefault = g.TenantId == null,
                    TermCount = g.Terms.Count
                })
                .OrderBy(g => g.SectorName)
                .ToListAsync(cancellationToken);

            return Ok(sectors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving glossary sectors");
            return StatusCode(500, Result.Fail("Error retrieving glossary sectors"));
        }
    }

    /// <summary>
    /// Get a sector with all its terms by sector key
    /// </summary>
    [HttpGet("sectors/{key}")]
    [ProducesResponseType(typeof(GlossarySectorDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSectorByKey(string key, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            // Prefer tenant-specific override, fall back to system default
            var glossary = await _dbContext.SafetyGlossaries
                .Include(g => g.Terms.OrderBy(t => t.EnglishTerm))
                .Where(g => g.SectorKey == key && (g.TenantId == tenantId || g.TenantId == null))
                .Where(g => g.IsActive)
                .OrderByDescending(g => g.TenantId.HasValue) // tenant-specific first (non-null)
                .FirstOrDefaultAsync(cancellationToken);

            if (glossary == null)
                return NotFound(new { message = $"Sector '{key}' not found" });

            var dto = new GlossarySectorDetailDto
            {
                Id = glossary.Id,
                SectorKey = glossary.SectorKey,
                SectorName = glossary.SectorName,
                SectorIcon = glossary.SectorIcon,
                IsSystemDefault = glossary.TenantId == null,
                Terms = glossary.Terms.Select(t => new GlossaryTermDto
                {
                    Id = t.Id,
                    EnglishTerm = t.EnglishTerm,
                    Category = t.Category,
                    IsCritical = t.IsCritical,
                    Translations = t.Translations
                }).ToList()
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sector {Key}", key);
            return StatusCode(500, new { message = "Error retrieving sector" });
        }
    }

    /// <summary>
    /// Get a sector with all its terms by glossary ID
    /// </summary>
    [HttpGet("sectors/by-id/{id:guid}")]
    [ProducesResponseType(typeof(GlossarySectorDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSectorById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            var glossary = await _dbContext.SafetyGlossaries
                .Include(g => g.Terms.OrderBy(t => t.EnglishTerm))
                .Where(g => g.Id == id && g.IsActive)
                .FirstOrDefaultAsync(cancellationToken);

            if (glossary == null)
                return NotFound(new { message = "Glossary not found" });

            // Must belong to current tenant or be a system default
            if (glossary.TenantId != null && glossary.TenantId != tenantId)
                return StatusCode(403, new { message = "Access denied" });

            var dto = new GlossarySectorDetailDto
            {
                Id = glossary.Id,
                SectorKey = glossary.SectorKey,
                SectorName = glossary.SectorName,
                SectorIcon = glossary.SectorIcon,
                IsSystemDefault = glossary.TenantId == null,
                Terms = glossary.Terms.Select(t => new GlossaryTermDto
                {
                    Id = t.Id,
                    EnglishTerm = t.EnglishTerm,
                    Category = t.Category,
                    IsCritical = t.IsCritical,
                    Translations = t.Translations
                }).ToList()
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving glossary {GlossaryId}", id);
            return StatusCode(500, new { message = "Error retrieving glossary" });
        }
    }

    /// <summary>
    /// Create a new tenant-specific glossary sector
    /// </summary>
    [HttpPost("sectors")]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(typeof(GlossarySectorDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSector(
        [FromBody] CreateSectorRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            if (string.IsNullOrWhiteSpace(request.SectorKey))
                return BadRequest(new { message = "Sector key is required" });

            if (string.IsNullOrWhiteSpace(request.SectorName))
                return BadRequest(new { message = "Sector name is required" });

            // Check for duplicate sector key within this tenant
            var exists = await _dbContext.SafetyGlossaries
                .AnyAsync(g => g.SectorKey == request.SectorKey && g.TenantId == tenantId,
                    cancellationToken);

            if (exists)
                return BadRequest(new { message = $"Sector with key '{request.SectorKey}' already exists for this tenant" });

            var glossary = new SafetyGlossary
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SectorKey = request.SectorKey,
                SectorName = request.SectorName,
                SectorIcon = request.SectorIcon,
                IsActive = true
            };

            _dbContext.SafetyGlossaries.Add(glossary);

            // If a system default sector exists with the same key, copy all its terms
            var systemDefault = await _dbContext.SafetyGlossaries
                .Include(g => g.Terms)
                .FirstOrDefaultAsync(g => g.SectorKey == request.SectorKey
                    && g.TenantId == null && g.IsActive,
                    cancellationToken);

            if (systemDefault != null && systemDefault.Terms.Any())
            {
                foreach (var sourceTerm in systemDefault.Terms)
                {
                    _dbContext.SafetyGlossaryTerms.Add(new SafetyGlossaryTerm
                    {
                        Id = Guid.NewGuid(),
                        GlossaryId = glossary.Id,
                        EnglishTerm = sourceTerm.EnglishTerm,
                        Category = sourceTerm.Category,
                        IsCritical = sourceTerm.IsCritical,
                        Translations = sourceTerm.Translations
                    });
                }

                _logger.LogInformation(
                    "Copied {Count} terms from system default sector '{Key}' to tenant override",
                    systemDefault.Terms.Count, request.SectorKey);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Build response with copied terms
            var terms = await _dbContext.SafetyGlossaryTerms
                .Where(t => t.GlossaryId == glossary.Id)
                .OrderBy(t => t.EnglishTerm)
                .Select(t => new GlossaryTermDto
                {
                    Id = t.Id,
                    EnglishTerm = t.EnglishTerm,
                    Category = t.Category,
                    IsCritical = t.IsCritical,
                    Translations = t.Translations
                })
                .ToListAsync(cancellationToken);

            var dto = new GlossarySectorDetailDto
            {
                Id = glossary.Id,
                SectorKey = glossary.SectorKey,
                SectorName = glossary.SectorName,
                SectorIcon = glossary.SectorIcon,
                IsSystemDefault = false,
                Terms = terms
            };

            return CreatedAtAction(nameof(GetSectorByKey), new { key = glossary.SectorKey }, dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating glossary sector");
            return StatusCode(500, new { message = "Error creating sector" });
        }
    }

    /// <summary>
    /// Update a sector's name and icon
    /// </summary>
    [HttpPut("sectors/{id:guid}")]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSector(
        Guid id,
        [FromBody] UpdateSectorRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            var glossary = await _dbContext.SafetyGlossaries
                .FirstOrDefaultAsync(g => g.Id == id
                    && (g.TenantId == tenantId || g.TenantId == null),
                    cancellationToken);

            if (glossary == null)
                return NotFound(new { message = "Sector not found" });

            // Don't allow editing system defaults directly — tenant must create an override
            if (glossary.TenantId == null)
                return BadRequest(new { message = "Cannot modify system default sectors. Create a tenant-specific override instead." });

            if (string.IsNullOrWhiteSpace(request.SectorName))
                return BadRequest(new { message = "Sector name is required" });

            glossary.SectorName = request.SectorName;
            glossary.SectorIcon = request.SectorIcon;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new { message = "Sector updated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating sector {SectorId}", id);
            return StatusCode(500, new { message = "Error updating sector" });
        }
    }

    /// <summary>
    /// Add a term to a glossary sector
    /// </summary>
    [HttpPost("sectors/{id:guid}/terms")]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(typeof(GlossaryTermDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddTerm(
        Guid id,
        [FromBody] CreateTermRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            var glossary = await _dbContext.SafetyGlossaries
                .FirstOrDefaultAsync(g => g.Id == id
                    && (g.TenantId == tenantId || g.TenantId == null),
                    cancellationToken);

            if (glossary == null)
                return NotFound(new { message = "Sector not found" });

            if (glossary.TenantId == null)
                return BadRequest(new { message = "Cannot modify system default sectors. Create a tenant-specific override instead." });

            if (string.IsNullOrWhiteSpace(request.EnglishTerm))
                return BadRequest(new { message = "English term is required" });

            if (string.IsNullOrWhiteSpace(request.Category))
                return BadRequest(new { message = "Category is required" });

            // Check for duplicate term within this glossary
            var termExists = await _dbContext.SafetyGlossaryTerms
                .AnyAsync(t => t.GlossaryId == id && t.EnglishTerm == request.EnglishTerm,
                    cancellationToken);

            if (termExists)
                return BadRequest(new { message = $"Term '{request.EnglishTerm}' already exists in this sector" });

            var term = new SafetyGlossaryTerm
            {
                Id = Guid.NewGuid(),
                GlossaryId = id,
                EnglishTerm = request.EnglishTerm,
                Category = request.Category,
                IsCritical = request.IsCritical,
                Translations = request.Translations
            };

            _dbContext.SafetyGlossaryTerms.Add(term);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var dto = new GlossaryTermDto
            {
                Id = term.Id,
                EnglishTerm = term.EnglishTerm,
                Category = term.Category,
                IsCritical = term.IsCritical,
                Translations = term.Translations
            };

            return Created($"/api/toolbox-talks/glossary/terms/{term.Id}", dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding term to sector {SectorId}", id);
            return StatusCode(500, new { message = "Error adding term" });
        }
    }

    /// <summary>
    /// Update a glossary term including its translations
    /// </summary>
    [HttpPut("terms/{id:guid}")]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTerm(
        Guid id,
        [FromBody] UpdateTermRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            var term = await _dbContext.SafetyGlossaryTerms
                .Include(t => t.Glossary)
                .FirstOrDefaultAsync(t => t.Id == id
                    && (t.Glossary.TenantId == tenantId || t.Glossary.TenantId == null),
                    cancellationToken);

            if (term == null)
                return NotFound(new { message = "Term not found" });

            if (term.Glossary.TenantId == null)
                return BadRequest(new { message = "Cannot modify system default sectors. Create a tenant-specific override instead." });

            if (string.IsNullOrWhiteSpace(request.EnglishTerm))
                return BadRequest(new { message = "English term is required" });

            if (string.IsNullOrWhiteSpace(request.Category))
                return BadRequest(new { message = "Category is required" });

            term.EnglishTerm = request.EnglishTerm;
            term.Category = request.Category;
            term.IsCritical = request.IsCritical;
            term.Translations = request.Translations;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new GlossaryTermDto
            {
                Id = term.Id,
                EnglishTerm = term.EnglishTerm,
                Category = term.Category,
                IsCritical = term.IsCritical,
                Translations = term.Translations
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating term {TermId}", id);
            return StatusCode(500, new { message = "Error updating term" });
        }
    }

    /// <summary>
    /// Soft delete a glossary term
    /// </summary>
    [HttpDelete("terms/{id:guid}")]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTerm(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            var term = await _dbContext.SafetyGlossaryTerms
                .Include(t => t.Glossary)
                .FirstOrDefaultAsync(t => t.Id == id
                    && (t.Glossary.TenantId == tenantId || t.Glossary.TenantId == null),
                    cancellationToken);

            if (term == null)
                return NotFound(new { message = "Term not found" });

            if (term.Glossary.TenantId == null)
                return BadRequest(new { message = "Cannot modify system default sectors. Create a tenant-specific override instead." });

            term.IsDeleted = true;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Glossary term {TermId} soft-deleted", id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting term {TermId}", id);
            return StatusCode(500, new { message = "Error deleting term" });
        }
    }

    /// <summary>
    /// Bulk import glossary terms from a CSV file
    /// </summary>
    [HttpPost("sectors/{id:guid}/terms/import")]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(typeof(ImportTermsResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [RequestSizeLimit(1_048_576)] // 1MB
    public async Task<IActionResult> ImportTerms(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "A CSV file is required" });

            var glossary = await _dbContext.SafetyGlossaries
                .FirstOrDefaultAsync(g => g.Id == id
                    && (g.TenantId == tenantId || g.TenantId == null),
                    cancellationToken);

            if (glossary == null)
                return NotFound(new { message = "Sector not found" });

            if (glossary.TenantId == null)
                return StatusCode(403, new { message = "Cannot modify system default glossaries" });

            // Load existing terms for duplicate check
            var existingTerms = await _dbContext.SafetyGlossaryTerms
                .Where(t => t.GlossaryId == id)
                .Select(t => t.EnglishTerm.ToLower())
                .ToListAsync(cancellationToken);

            var existingSet = new HashSet<string>(existingTerms);

            // Read CSV content
            string content;
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                content = await reader.ReadToEndAsync(cancellationToken);
            }

            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length < 2)
                return BadRequest(new { message = "CSV file must contain a header row and at least one data row" });

            // Validate header
            var header = lines[0].Trim().TrimEnd('\r');
            var expectedHeader = "english_term,category,is_critical,fr,pl,ro,uk,pt,es,lt,de,lv";
            if (!string.Equals(header, expectedHeader, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = $"Invalid CSV header. Expected: {expectedHeader}" });

            var dataLines = lines.Skip(1).ToArray();

            if (dataLines.Length > 500)
                return BadRequest(new { message = "Maximum 500 rows per import" });

            var validCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "equipment", "regulatory", "procedure", "prohibition", "hazard", "emergency" };

            var languageCodes = new[] { "fr", "pl", "ro", "uk", "pt", "es", "lt", "de", "lv" };

            var imported = 0;
            var skipped = 0;
            var errors = new List<string>();

            for (var i = 0; i < dataLines.Length; i++)
            {
                var line = dataLines[i].Trim().TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var rowNumber = i + 2; // 1-indexed, skip header
                var fields = line.Split(',');

                if (fields.Length < 3)
                {
                    errors.Add($"Row {rowNumber}: insufficient columns (need at least english_term, category, is_critical)");
                    continue;
                }

                var englishTerm = fields[0].Trim();
                var category = fields[1].Trim();
                var isCriticalStr = fields[2].Trim();

                if (string.IsNullOrWhiteSpace(englishTerm))
                {
                    errors.Add($"Row {rowNumber}: missing english_term");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(category))
                {
                    errors.Add($"Row {rowNumber}: missing category");
                    continue;
                }

                if (!validCategories.Contains(category))
                {
                    errors.Add($"Row {rowNumber}: invalid category '{category}' (must be equipment/regulatory/procedure/prohibition/hazard/emergency)");
                    continue;
                }

                if (!bool.TryParse(isCriticalStr, out var isCritical))
                {
                    errors.Add($"Row {rowNumber}: invalid is_critical value '{isCriticalStr}' (must be true/false)");
                    continue;
                }

                // Duplicate check (case-insensitive)
                if (existingSet.Contains(englishTerm.ToLower()))
                {
                    skipped++;
                    continue;
                }

                // Build translations JSON from language columns
                var translations = new Dictionary<string, string>();
                for (var j = 0; j < languageCodes.Length; j++)
                {
                    var fieldIndex = j + 3;
                    if (fieldIndex < fields.Length)
                    {
                        var value = fields[fieldIndex].Trim();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            translations[languageCodes[j]] = value;
                        }
                    }
                }

                var translationsJson = JsonSerializer.Serialize(translations);

                _dbContext.SafetyGlossaryTerms.Add(new SafetyGlossaryTerm
                {
                    Id = Guid.NewGuid(),
                    GlossaryId = id,
                    EnglishTerm = englishTerm,
                    Category = category.ToLower(),
                    IsCritical = isCritical,
                    Translations = translationsJson
                });

                existingSet.Add(englishTerm.ToLower());
                imported++;
            }

            if (imported > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation(
                "CSV import for glossary {GlossaryId}: {Imported} imported, {Skipped} skipped, {Errors} errors",
                id, imported, skipped, errors.Count);

            return Ok(new ImportTermsResultDto
            {
                Imported = imported,
                Skipped = skipped,
                Errors = errors
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing terms to glossary {GlossaryId}", id);
            return StatusCode(500, new { message = "Error importing terms" });
        }
    }
}
