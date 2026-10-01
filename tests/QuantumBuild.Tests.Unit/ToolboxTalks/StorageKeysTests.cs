using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;

namespace QuantumBuild.Tests.Unit.ToolboxTalks;

public class StorageKeysTests
{
    private static readonly Guid T = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherT = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void BrandingKeyUnderOwnTenant_IsTrue() =>
        StorageKeys.IsTenantBrandingKey(T, $"{T}/branding/logo-x.png").Should().BeTrue();

    [Fact]
    public void NestedBrandingFolderUnderOtherFolder_IsFalse() =>
        StorageKeys.IsTenantBrandingKey(T, $"{T}/talks/branding/x").Should().BeFalse();

    [Fact]
    public void BrandingKeyOfOtherTenant_IsFalse() =>
        StorageKeys.IsTenantBrandingKey(T, $"{OtherT}/branding/x").Should().BeFalse();

    [Fact]
    public void FolderWithBrandingAsPrefixOnly_IsFalse() =>
        StorageKeys.IsTenantBrandingKey(T, $"{T}/brandingx/y").Should().BeFalse();

    // R2 keys are case-sensitive and we only ever write the lowercase folder, so {T}/Branding/ is a
    // different, never-written prefix. It is not protected (ordinal comparison), which is correct.
    [Fact]
    public void UppercaseBrandingFolder_IsFalse() =>
        StorageKeys.IsTenantBrandingKey(T, $"{T}/Branding/x").Should().BeFalse();
}
