using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services;

/// <summary>
/// Manages the system-level translation pipeline version record, providing an auditable
/// snapshot of which providers and thresholds were active during each validation run.
/// </summary>
public class PipelineVersionService : IPipelineVersionService
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly TranslationValidationSettings _settings;
    private readonly ILogger<PipelineVersionService> _logger;

    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public PipelineVersionService(
        IToolboxTalksDbContext dbContext,
        IOptions<TranslationValidationSettings> settings,
        ILogger<PipelineVersionService> logger)
    {
        _dbContext = dbContext;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PipelineVersion?> GetActiveAsync(CancellationToken ct = default)
    {
        return await _dbContext.PipelineVersions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.IsActive && !v.IsDeleted, ct);
    }

    /// <inheritdoc />
    public async Task<PipelineVersion> GetOrCreateCurrentAsync(CancellationToken ct = default)
    {
        var currentVersionLabel = _settings.PipelineVersion;

        // Return existing active record if it matches the configured version string
        var existing = await _dbContext.PipelineVersions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.IsActive && v.Version == currentVersionLabel && !v.IsDeleted, ct);

        if (existing != null)
        {
            _logger.LogDebug(
                "Pipeline version {Version} (hash {Hash}) already active — no action needed",
                existing.Version, existing.Hash);
            return existing;
        }

        return await CreateNewVersionAsync(currentVersionLabel, ct);
    }

    /// <inheritdoc />
    public async Task<PipelineVersion> CreateNewVersionAsync(string version, CancellationToken ct = default)
    {
        var componentsJson = BuildComponentsJson();
        var hash = ComputeHash(componentsJson);

        _logger.LogInformation(
            "Creating new pipeline version record: version={Version}, hash={Hash}",
            version, hash);

        // Deactivate any previously active record
        var previousActive = await _dbContext.PipelineVersions
            .IgnoreQueryFilters()
            .Where(v => v.IsActive && !v.IsDeleted)
            .ToListAsync(ct);

        foreach (var prev in previousActive)
        {
            prev.IsActive = false;
        }

        var newVersion = new PipelineVersion
        {
            Id = Guid.NewGuid(),
            Version = version,
            Hash = hash,
            ComponentsJson = componentsJson,
            ComputedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        _dbContext.PipelineVersions.Add(newVersion);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Pipeline version {Version} (hash {Hash}) created and set as active",
            version, hash);

        return newVersion;
    }

    /// <summary>
    /// Builds the ComponentsJson snapshot from the current TranslationValidationSettings.
    /// The hash is derived from this JSON, so the same configuration always produces the same hash.
    /// </summary>
    private string BuildComponentsJson()
    {
        var components = new
        {
            round1_a = _settings.Round1AModel,
            round1_b = "deepl",
            round2_c = _settings.Gemini.Model,
            round3_d = _settings.Round3DModel,
            thresholds = new
            {
                @default = _settings.DefaultThreshold,
                safety_critical_bump = _settings.SafetyCriticalBump,
                agreement = _settings.AgreementThreshold
            },
            max_rounds = _settings.MaxRounds,
            prompt_version = _settings.PromptVersion,
            processing_mode = _settings.ProcessingMode
        };

        return JsonSerializer.Serialize(components, SnakeCaseOptions);
    }

    /// <inheritdoc />
    public async Task<PipelineChangeRecord> CreateChangeRecordAsync(
        CreatePipelineChangeRecordRequest request, CancellationToken ct = default)
    {
        // Capture the current active version before bumping
        var previousActive = await GetActiveAsync(ct);

        // Create a new pipeline version for this change
        var newVersion = await CreateNewVersionAsync(request.NewVersionLabel, ct);

        var changeId = await GenerateChangeIdAsync(ct);

        var record = new PipelineChangeRecord
        {
            Id = Guid.NewGuid(),
            ChangeId = changeId,
            Component = request.Component,
            ChangeFrom = request.ChangeFrom,
            ChangeTo = request.ChangeTo,
            Justification = request.Justification,
            ImpactAssessment = request.ImpactAssessment,
            PriorModulesAction = request.PriorModulesAction,
            Approver = request.Approver,
            DeployedAt = DateTimeOffset.UtcNow,
            PipelineVersionId = newVersion.Id,
            PreviousPipelineVersionId = previousActive?.Id
        };

        _dbContext.PipelineChangeRecords.Add(record);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Pipeline change record {ChangeId} created, new version {Version} (hash {Hash})",
            changeId, newVersion.Version, newVersion.Hash);

        return record;
    }

    /// <summary>
    /// Generates a system-wide sequential ChangeId: "CR-0001", "CR-0002", etc.
    /// Includes soft-deleted records in the scan so IDs are never reused.
    /// </summary>
    private async Task<string> GenerateChangeIdAsync(CancellationToken ct)
    {
        var existingIds = await _dbContext.PipelineChangeRecords
            .IgnoreQueryFilters()
            .Where(cr => cr.ChangeId.StartsWith("CR-"))
            .Select(cr => cr.ChangeId)
            .ToListAsync(ct);

        int maxSuffix = 0;
        foreach (var id in existingIds)
        {
            var suffix = id[3..]; // "CR-0001" → "0001"
            if (int.TryParse(suffix, out var num) && num > maxSuffix)
                maxSuffix = num;
        }

        return $"CR-{maxSuffix + 1:D4}";
    }

    /// <summary>
    /// Computes "sha256:" + first 12 hex chars of the SHA-256 hash of the input string.
    /// </summary>
    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var hex = Convert.ToHexString(bytes).ToLowerInvariant();
        return $"sha256:{hex[..12]}";
    }
}
