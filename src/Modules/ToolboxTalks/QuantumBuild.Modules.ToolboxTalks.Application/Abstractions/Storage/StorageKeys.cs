namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;

/// <summary>
/// Pure helpers over the R2 key layout ({tenantId}/{folder}/{fileName}). Shared by the real and fake
/// storage services so the tenant-reset exclusion rule has exactly one definition.
/// </summary>
public static class StorageKeys
{
    public const string BrandingFolder = "branding";

    /// <summary>
    /// True when <paramref name="key"/> lives directly under {tenantId}/branding/. Ordinal (case-sensitive)
    /// comparison: R2 keys are case-sensitive and only the lowercase folder is ever written.
    /// </summary>
    public static bool IsTenantBrandingKey(Guid tenantId, string key) =>
        key.StartsWith($"{tenantId}/{BrandingFolder}/", StringComparison.Ordinal);
}
