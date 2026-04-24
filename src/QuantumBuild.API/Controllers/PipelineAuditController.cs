using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.SafetyTermRegistry;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.API.Controllers;

/// <summary>
/// Translation Pipeline Audit — deviations, module outcomes, change records, and dashboard.
/// Route base: /api/toolbox-talks/pipeline
/// </summary>
[ApiController]
[Route("api/toolbox-talks/pipeline")]
[Authorize(Policy = "Learnings.View")]
public class PipelineAuditController : ControllerBase
{
    private readonly ITranslationDeviationService _deviationService;
    private readonly IPipelineAuditQueryService _auditQueryService;
    private readonly IPipelineVersionService _pipelineVersionService;
    private readonly ICurrentUserService _currentUser;
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ISafetyTermRegistryService _registryService;
    private readonly ILogger<PipelineAuditController> _logger;

    public PipelineAuditController(
        ITranslationDeviationService deviationService,
        IPipelineAuditQueryService auditQueryService,
        IPipelineVersionService pipelineVersionService,
        ICurrentUserService currentUser,
        IToolboxTalksDbContext dbContext,
        ISafetyTermRegistryService registryService,
        ILogger<PipelineAuditController> logger)
    {
        _deviationService = deviationService;
        _auditQueryService = auditQueryService;
        _pipelineVersionService = pipelineVersionService;
        _currentUser = currentUser;
        _dbContext = dbContext;
        _registryService = registryService;
        _logger = logger;
    }

    // ─── Dashboard ────────────────────────────────────────────────────────────

    /// <summary>
    /// Summary dashboard — deviation counts, change records, locked terms, active pipeline.
    /// SuperUser may pass X-Tenant-Id header for tenant-scoped counts.
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(PipelineAuditDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        try
        {
            Guid? tenantOverride = null;
            if (_currentUser.IsSuperUser && Request.Headers.TryGetValue("X-Tenant-Id", out var headerValue)
                && Guid.TryParse(headerValue.ToString(), out var parsedTenantId))
            {
                tenantOverride = parsedTenantId;
            }

            var dashboard = await _auditQueryService.GetDashboardSummaryAsync(tenantOverride, cancellationToken);
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading pipeline audit dashboard");
            return StatusCode(500, Result.Fail("Error loading dashboard"));
        }
    }

    // ─── Module Outcomes ─────────────────────────────────────────────────────

    /// <summary>
    /// Paginated list of completed validation runs as module outcomes.
    /// </summary>
    [HttpGet("runs")]
    [ProducesResponseType(typeof(PaginatedList<ModuleOutcomeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetModuleOutcomes(
        [FromQuery] string? outcome,
        [FromQuery] string? languageCode,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidationOutcome? parsedOutcome = null;
            if (!string.IsNullOrEmpty(outcome) && Enum.TryParse<ValidationOutcome>(outcome, out var parsed))
                parsedOutcome = parsed;

            Guid? tenantOverride = null;
            if (_currentUser.IsSuperUser && Request.Headers.TryGetValue("X-Tenant-Id", out var headerValue)
                && Guid.TryParse(headerValue.ToString(), out var parsedTenantId))
            {
                tenantOverride = parsedTenantId;
            }

            var result = await _auditQueryService.GetModuleOutcomesAsync(
                tenantOverride, parsedOutcome, languageCode, page, pageSize, cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading module outcomes");
            return StatusCode(500, Result.Fail("Error loading module outcomes"));
        }
    }

    // ─── Deviations ───────────────────────────────────────────────────────────

    /// <summary>
    /// Paginated list of deviations for the current tenant.
    /// </summary>
    [HttpGet("deviations")]
    [ProducesResponseType(typeof(PaginatedList<TranslationDeviationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDeviations(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            DeviationStatus? parsedStatus = null;
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<DeviationStatus>(status, out var parsed))
                parsedStatus = parsed;

            var result = await _deviationService.GetPagedAsync(parsedStatus, page, pageSize, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading deviations");
            return StatusCode(500, Result.Fail("Error loading deviations"));
        }
    }

    /// <summary>
    /// Get a single deviation by ID.
    /// </summary>
    [HttpGet("deviations/{id:guid}")]
    [ProducesResponseType(typeof(TranslationDeviationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDeviation(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var dto = await _deviationService.GetByIdAsync(id, cancellationToken);
            return dto == null ? NotFound() : Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading deviation {Id}", id);
            return StatusCode(500, Result.Fail("Error loading deviation"));
        }
    }

    /// <summary>
    /// Create a new deviation record.
    /// </summary>
    [HttpPost("deviations")]
    [Authorize(Policy = "Learnings.Manage")]
    [ProducesResponseType(typeof(TranslationDeviationDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateDeviation(
        [FromBody] CreateDeviationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Nature))
                return BadRequest(new { message = "Nature is required" });

            if (string.IsNullOrWhiteSpace(request.RootCauseCategory))
                return BadRequest(new { message = "RootCauseCategory is required" });

            var dto = await _deviationService.CreateAsync(request, cancellationToken);

            _logger.LogInformation(
                "Deviation {DeviationId} created by {User}", dto.DeviationId, _currentUser.UserName);

            return CreatedAtAction(nameof(GetDeviation), new { id = dto.Id }, dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating deviation");
            return StatusCode(500, Result.Fail("Error creating deviation"));
        }
    }

    /// <summary>
    /// Update deviation status (Open → InProgress → Closed).
    /// </summary>
    [HttpPut("deviations/{id:guid}/status")]
    [Authorize(Policy = "Learnings.Manage")]
    [ProducesResponseType(typeof(TranslationDeviationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDeviationStatus(
        Guid id,
        [FromBody] UpdateDeviationStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!Enum.TryParse<DeviationStatus>(request.Status, out var status))
                return BadRequest(new { message = $"Invalid status '{request.Status}'" });

            var dto = await _deviationService.UpdateStatusAsync(id, status, request.ClosedBy, cancellationToken);
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating deviation status for {Id}", id);
            return StatusCode(500, Result.Fail("Error updating deviation status"));
        }
    }

    // ─── Pipeline Changes ─────────────────────────────────────────────────────

    /// <summary>
    /// Paginated list of all pipeline change records (system-wide, append-only).
    /// </summary>
    [HttpGet("changes")]
    [ProducesResponseType(typeof(PaginatedList<PipelineChangeRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChangeRecords(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _auditQueryService.GetChangeRecordsAsync(page, pageSize, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading change records");
            return StatusCode(500, Result.Fail("Error loading change records"));
        }
    }

    /// <summary>
    /// Create a new pipeline change record (SuperUser only).
    /// Also bumps the active pipeline version.
    /// </summary>
    [HttpPost("changes")]
    [ProducesResponseType(typeof(PipelineChangeRecordDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateChangeRecord(
        [FromBody] CreatePipelineChangeRecordRequest request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsSuperUser)
            return Forbid();

        try
        {
            if (string.IsNullOrWhiteSpace(request.Component))
                return BadRequest(new { message = "Component is required" });
            if (string.IsNullOrWhiteSpace(request.Justification))
                return BadRequest(new { message = "Justification is required" });
            if (string.IsNullOrWhiteSpace(request.NewVersionLabel))
                return BadRequest(new { message = "NewVersionLabel is required" });

            var record = await _pipelineVersionService.CreateChangeRecordAsync(request, cancellationToken);

            _logger.LogInformation(
                "Pipeline change record {ChangeId} created by SuperUser {User}",
                record.ChangeId, _currentUser.UserName);

            var dto = new PipelineChangeRecordDto
            {
                Id = record.Id,
                ChangeId = record.ChangeId,
                Component = record.Component,
                ChangeFrom = record.ChangeFrom,
                ChangeTo = record.ChangeTo,
                Justification = record.Justification,
                ImpactAssessment = record.ImpactAssessment,
                PriorModulesAction = record.PriorModulesAction,
                Approver = record.Approver,
                DeployedAt = record.DeployedAt,
                PipelineVersionId = record.PipelineVersionId,
                PreviousPipelineVersionId = record.PreviousPipelineVersionId,
                CreatedAt = record.CreatedAt
            };

            return CreatedAtAction(nameof(GetChangeRecords), dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating pipeline change record");
            return StatusCode(500, Result.Fail("Error creating change record"));
        }
    }

    // ─── Pipeline Version ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the currently active pipeline version.
    /// </summary>
    [HttpGet("version")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveVersion(CancellationToken cancellationToken)
    {
        try
        {
            var version = await _auditQueryService.GetActivePipelineVersionAsync(cancellationToken);
            if (version == null)
                return Ok(new { version = "—", hash = "—", computedAt = (DateTimeOffset?)null });

            return Ok(new
            {
                id = version.Id,
                version = version.Version,
                hash = version.Hash,
                computedAt = version.ComputedAt,
                componentsJson = version.ComponentsJson
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading active pipeline version");
            return StatusCode(500, Result.Fail("Error loading pipeline version"));
        }
    }

    // ─── Term Gate ────────────────────────────────────────────────────────────

    /// <summary>
    /// Summary of the term database: total terms, critical terms, terms by sector, and language coverage.
    /// Includes system defaults + tenant overrides, deduplicated with tenant taking precedence.
    /// </summary>
    [HttpGet("term-gate/summary")]
    [ProducesResponseType(typeof(TermGateSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTermGateSummary(CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUser.TenantId;
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            var allTerms = await _dbContext.SafetyGlossaryTerms
                .IgnoreQueryFilters()
                .Include(t => t.Glossary)
                .Where(t => !t.IsDeleted
                    && !t.Glossary.IsDeleted
                    && (t.Glossary.TenantId == tenantId || t.Glossary.TenantId == null))
                .ToListAsync(cancellationToken);

            // Deduplicate per (SectorKey, EnglishTerm), prefer tenant override
            var deduped = allTerms
                .GroupBy(t => $"{t.Glossary.SectorKey}||{t.EnglishTerm.ToLowerInvariant()}")
                .Select(g => g.FirstOrDefault(t => t.Glossary.TenantId == tenantId) ?? g.First())
                .ToList();

            var bySector = deduped
                .GroupBy(t => t.Glossary.SectorKey)
                .Select(g =>
                {
                    var tenantTerm = g.FirstOrDefault(t => t.Glossary.TenantId == tenantId);
                    var sectorName = (tenantTerm ?? g.First()).Glossary.SectorName;
                    return new TermGateSectorSummary
                    {
                        SectorKey = g.Key,
                        SectorName = sectorName,
                        TermCount = g.Count()
                    };
                })
                .OrderBy(s => s.SectorName)
                .ToList();

            var languagesWithCoverage = deduped
                .SelectMany(t =>
                {
                    if (string.IsNullOrWhiteSpace(t.Translations)) return Enumerable.Empty<string>();
                    try
                    {
                        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(t.Translations, jsonOptions);
                        return dict?.Where(kv => !string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key)
                               ?? Enumerable.Empty<string>();
                    }
                    catch { return Enumerable.Empty<string>(); }
                })
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            return Ok(new TermGateSummaryDto
            {
                TotalTerms = deduped.Count,
                CriticalTerms = deduped.Count(t => t.IsCritical),
                TermsBySector = bySector,
                LanguagesWithCoverage = languagesWithCoverage,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading term gate summary");
            return StatusCode(500, Result.Fail("Error loading term gate summary"));
        }
    }

    /// <summary>
    /// Run a term gate check: verify that glossary terms found in the source appear correctly in the target,
    /// and that no known-forbidden variants are present.
    /// </summary>
    [HttpPost("term-gate/check")]
    [ProducesResponseType(typeof(TermGateCheckResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckTermGate(
        [FromBody] TermGateCheckRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SourceText) ||
            string.IsNullOrWhiteSpace(request.TargetText) ||
            string.IsNullOrWhiteSpace(request.LanguageCode) ||
            string.IsNullOrWhiteSpace(request.SectorKey))
        {
            return BadRequest(new { message = "SourceText, TargetText, LanguageCode, and SectorKey are required" });
        }

        try
        {
            var tenantId = _currentUser.TenantId;
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            // Load glossary terms — same pattern as TranslationValidationJob.LoadGlossaryTermsAsync
            var allTerms = await _dbContext.SafetyGlossaryTerms
                .IgnoreQueryFilters()
                .Include(t => t.Glossary)
                .Where(t => !t.IsDeleted
                    && !t.Glossary.IsDeleted
                    && t.Glossary.SectorKey == request.SectorKey
                    && (t.Glossary.TenantId == tenantId || t.Glossary.TenantId == null))
                .ToListAsync(cancellationToken);

            // Prefer tenant override per English term
            var grouped = allTerms.GroupBy(t => t.EnglishTerm.ToLowerInvariant());

            // Registry scan for forbidden patterns (once per request)
            var languageName = MapLanguageCodeToName(request.LanguageCode);
            var registryScan = _registryService.Scan(request.TargetText, languageName);

            var failures = new List<TermGateFailure>();
            var passingTerms = new List<TermGatePassingTerm>();
            var checkedTermIds = new HashSet<Guid>();

            foreach (var group in grouped)
            {
                var term = group.FirstOrDefault(t => t.Glossary.TenantId == tenantId) ?? group.First();

                // Only process terms that appear in the source text
                if (!request.SourceText.Contains(term.EnglishTerm, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Get approved translation for this language
                if (string.IsNullOrWhiteSpace(term.Translations)) continue;

                Dictionary<string, string>? translations;
                try
                {
                    translations = JsonSerializer.Deserialize<Dictionary<string, string>>(term.Translations, jsonOptions);
                }
                catch (JsonException) { continue; }

                if (translations == null
                    || !translations.TryGetValue(request.LanguageCode, out var approvedTranslation)
                    || string.IsNullOrWhiteSpace(approvedTranslation))
                {
                    continue; // No approved translation for this language — skip
                }

                checkedTermIds.Add(term.Id);

                // Check 1: missing_approved
                bool approvedFound = request.TargetText.Contains(approvedTranslation, StringComparison.OrdinalIgnoreCase);

                if (!approvedFound)
                {
                    failures.Add(new TermGateFailure
                    {
                        TermId = term.Id,
                        EnglishTerm = term.EnglishTerm,
                        ExpectedTranslation = approvedTranslation,
                        FailureReason = "missing_approved",
                    });
                }
                else
                {
                    passingTerms.Add(new TermGatePassingTerm
                    {
                        TermId = term.Id,
                        EnglishTerm = term.EnglishTerm,
                        ApprovedTranslation = approvedTranslation,
                    });
                }

                // Check 2: forbidden_present — link registry violations to this glossary term
                foreach (var violation in registryScan.Violations)
                {
                    var termLower = term.EnglishTerm.ToLowerInvariant();
                    var sourceLower = violation.SourceTerm.ToLowerInvariant();

                    if (sourceLower.Contains(termLower) || termLower.Contains(sourceLower))
                    {
                        failures.Add(new TermGateFailure
                        {
                            TermId = term.Id,
                            EnglishTerm = term.EnglishTerm,
                            ExpectedTranslation = approvedTranslation,
                            FailureReason = "forbidden_present",
                            ForbiddenTermFound = violation.FoundBadPattern,
                        });
                    }
                }
            }

            return Ok(new TermGateCheckResult
            {
                Passed = failures.Count == 0,
                CheckedCount = checkedTermIds.Count,
                Failures = failures,
                PassingTerms = passingTerms,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking term gate for sector {Sector} language {Lang}",
                request.SectorKey, request.LanguageCode);
            return StatusCode(500, Result.Fail("Error checking term gate"));
        }
    }

    private static string MapLanguageCodeToName(string code) => code.ToLowerInvariant() switch
    {
        "pl" => "Polish",
        "ro" => "Romanian",
        "pt" => "Portuguese",
        "es" => "Spanish",
        "fr" => "French",
        "uk" => "Ukrainian",
        "lt" => "Lithuanian",
        "de" => "German",
        "lv" => "Latvian",
        _ => code,
    };
}
