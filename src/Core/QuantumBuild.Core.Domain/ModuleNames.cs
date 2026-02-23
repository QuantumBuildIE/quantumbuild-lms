namespace QuantumBuild.Core.Domain;

/// <summary>
/// Constants for available module names in the system.
/// Used by TenantModule to define which modules a tenant has access to.
/// </summary>
public static class ModuleNames
{
    public const string Learnings = "Learnings";
    public const string LessonParser = "LessonParser";

    /// <summary>
    /// Returns all known module names.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = new[] { Learnings, LessonParser };
}
