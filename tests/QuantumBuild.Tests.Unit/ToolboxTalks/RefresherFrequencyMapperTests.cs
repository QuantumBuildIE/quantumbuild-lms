using FluentAssertions;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Domain.Helpers;
using Xunit;

namespace QuantumBuild.Tests.Unit.ToolboxTalks;

public class RefresherFrequencyMapperTests
{
    // ── ToLegacyFrequency ──────────────────────────────────────────────────────

    [Fact]
    public void ToLegacyFrequency_WhenNotRequired_ReturnsOnce()
    {
        RefresherFrequencyMapper.ToLegacyFrequency(false, 12).Should().Be(ToolboxTalkFrequency.Once);
        RefresherFrequencyMapper.ToLegacyFrequency(false, 1).Should().Be(ToolboxTalkFrequency.Once);
        RefresherFrequencyMapper.ToLegacyFrequency(false, 0).Should().Be(ToolboxTalkFrequency.Once);
    }

    [Fact]
    public void ToLegacyFrequency_Monthly_ReturnsMonthly()
    {
        RefresherFrequencyMapper.ToLegacyFrequency(true, 1).Should().Be(ToolboxTalkFrequency.Monthly);
    }

    [Fact]
    public void ToLegacyFrequency_Quarterly_RoundsToMonthly()
    {
        RefresherFrequencyMapper.ToLegacyFrequency(true, 2).Should().Be(ToolboxTalkFrequency.Monthly);
        RefresherFrequencyMapper.ToLegacyFrequency(true, 3).Should().Be(ToolboxTalkFrequency.Monthly);
    }

    [Fact]
    public void ToLegacyFrequency_Annually_ReturnsAnnually()
    {
        RefresherFrequencyMapper.ToLegacyFrequency(true, 12).Should().Be(ToolboxTalkFrequency.Annually);
        RefresherFrequencyMapper.ToLegacyFrequency(true, 24).Should().Be(ToolboxTalkFrequency.Annually);
    }

    [Fact]
    public void ToLegacyFrequency_MidRange_ClosestBucketIsMonthly()
    {
        // 4–11 months: rounds to Monthly (closest legacy bucket)
        RefresherFrequencyMapper.ToLegacyFrequency(true, 4).Should().Be(ToolboxTalkFrequency.Monthly);
        RefresherFrequencyMapper.ToLegacyFrequency(true, 6).Should().Be(ToolboxTalkFrequency.Monthly);
        RefresherFrequencyMapper.ToLegacyFrequency(true, 11).Should().Be(ToolboxTalkFrequency.Monthly);
    }

    [Fact]
    public void ToLegacyFrequency_ZeroInterval_ClosestBucketIsMonthly()
    {
        // Edge case: required but zero months
        RefresherFrequencyMapper.ToLegacyFrequency(true, 0).Should().Be(ToolboxTalkFrequency.Monthly);
    }

    // ── ToCanonicalFields ──────────────────────────────────────────────────────

    [Fact]
    public void ToCanonicalFields_Once_ReturnsFalseAndPreservesInterval()
    {
        var (req, months) = RefresherFrequencyMapper.ToCanonicalFields(ToolboxTalkFrequency.Once, 6);
        req.Should().BeFalse();
        months.Should().Be(6); // existing interval preserved
    }

    [Fact]
    public void ToCanonicalFields_Once_DefaultInterval()
    {
        var (req, months) = RefresherFrequencyMapper.ToCanonicalFields(ToolboxTalkFrequency.Once);
        req.Should().BeFalse();
        months.Should().Be(12); // default
    }

    [Fact]
    public void ToCanonicalFields_Monthly_ReturnsTrueAnd1()
    {
        var (req, months) = RefresherFrequencyMapper.ToCanonicalFields(ToolboxTalkFrequency.Monthly);
        req.Should().BeTrue();
        months.Should().Be(1);
    }

    [Fact]
    public void ToCanonicalFields_Annually_ReturnsTrueAnd12()
    {
        var (req, months) = RefresherFrequencyMapper.ToCanonicalFields(ToolboxTalkFrequency.Annually);
        req.Should().BeTrue();
        months.Should().Be(12);
    }

    [Fact]
    public void ToCanonicalFields_Weekly_ReturnsFalseAndPreservesInterval()
    {
        // Weekly has no months equivalent — maps to no-refresher, preserving existing interval
        var (req, months) = RefresherFrequencyMapper.ToCanonicalFields(ToolboxTalkFrequency.Weekly, 3);
        req.Should().BeFalse();
        months.Should().Be(3);
    }

    // ── FromWizardFrequencyString ──────────────────────────────────────────────

    [Fact]
    public void FromWizardFrequencyString_Once_ReturnsFalse()
    {
        var (req, months) = RefresherFrequencyMapper.FromWizardFrequencyString("Once");
        req.Should().BeFalse();
        months.Should().Be(12);
    }

    [Fact]
    public void FromWizardFrequencyString_Monthly_ReturnsTrueAnd1()
    {
        var (req, months) = RefresherFrequencyMapper.FromWizardFrequencyString("Monthly");
        req.Should().BeTrue();
        months.Should().Be(1);
    }

    [Fact]
    public void FromWizardFrequencyString_Quarterly_ReturnsTrueAnd3()
    {
        var (req, months) = RefresherFrequencyMapper.FromWizardFrequencyString("Quarterly");
        req.Should().BeTrue();
        months.Should().Be(3);
    }

    [Fact]
    public void FromWizardFrequencyString_Annually_ReturnsTrueAnd12()
    {
        var (req, months) = RefresherFrequencyMapper.FromWizardFrequencyString("Annually");
        req.Should().BeTrue();
        months.Should().Be(12);
    }

    [Fact]
    public void FromWizardFrequencyString_Null_ReturnsFalseWithDefaultInterval()
    {
        var (req, months) = RefresherFrequencyMapper.FromWizardFrequencyString(null);
        req.Should().BeFalse();
        months.Should().Be(12);
    }

    [Fact]
    public void FromWizardFrequencyString_Unrecognised_ReturnsFalseWithDefaultInterval()
    {
        var (req, months) = RefresherFrequencyMapper.FromWizardFrequencyString("Biennial");
        req.Should().BeFalse();
        months.Should().Be(12);
    }

    [Fact]
    public void FromWizardFrequencyString_Once_PreservesExistingInterval()
    {
        // Once with an existing interval — interval is preserved
        var (req, months) = RefresherFrequencyMapper.FromWizardFrequencyString("Once", existingIntervalMonths: 3);
        req.Should().BeFalse();
        months.Should().Be(3);
    }

    // ── Round-trip ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Monthly", ToolboxTalkFrequency.Monthly)]
    [InlineData("Quarterly", ToolboxTalkFrequency.Monthly)] // Quarterly rounds to Monthly
    [InlineData("Annually", ToolboxTalkFrequency.Annually)]
    [InlineData("Once", ToolboxTalkFrequency.Once)]
    public void WizardString_RoundTrip_ToLegacyFrequency(string wizardValue, ToolboxTalkFrequency expectedFrequency)
    {
        var (req, months) = RefresherFrequencyMapper.FromWizardFrequencyString(wizardValue);
        var legacy = RefresherFrequencyMapper.ToLegacyFrequency(req, months);
        legacy.Should().Be(expectedFrequency);
    }
}
