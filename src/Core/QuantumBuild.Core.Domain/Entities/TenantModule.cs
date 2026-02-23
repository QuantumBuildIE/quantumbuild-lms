using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Core.Domain.Entities;

/// <summary>
/// Represents a module enabled for a specific tenant.
/// Each tenant can have multiple modules assigned to control feature access.
/// </summary>
public class TenantModule : TenantEntity
{
    /// <summary>
    /// Name of the module (e.g., "Learnings", "LessonParser").
    /// Use <see cref="ModuleNames"/> constants.
    /// </summary>
    public string ModuleName { get; set; } = string.Empty;
}
