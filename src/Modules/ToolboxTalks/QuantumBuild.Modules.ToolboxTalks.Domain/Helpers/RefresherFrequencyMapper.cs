using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Helpers;

/// <summary>
/// Bidirectional mapping between the legacy <see cref="ToolboxTalkFrequency"/> enum and
/// the canonical refresher fields <c>RequiresRefresher</c> + <c>RefresherIntervalMonths</c>.
///
/// Both representations live on ToolboxTalk for historical reasons. The new wizard treats
/// RequiresRefresher/RefresherIntervalMonths as canonical; the legacy Frequency enum is kept
/// in sync via this mapper so older code paths (admin list filter, dashboard breakdown,
/// old edit form) continue to work correctly. See BACKLOG §5.24.
///
/// Long-term direction: full removal of Frequency from ToolboxTalk once the old wizard is
/// decommissioned. See BACKLOG §7.1.
/// </summary>
public static class RefresherFrequencyMapper
{
    /// <summary>
    /// Convert canonical refresher fields to the legacy Frequency enum.
    /// Quarterly (3 months) maps to Monthly — the legacy enum has no Quarterly value.
    /// </summary>
    public static ToolboxTalkFrequency ToLegacyFrequency(bool requiresRefresher, int intervalMonths)
    {
        if (!requiresRefresher)
            return ToolboxTalkFrequency.Once;

        return intervalMonths switch
        {
            1 => ToolboxTalkFrequency.Monthly,
            2 or 3 => ToolboxTalkFrequency.Monthly,   // Quarterly (3 months) rounds to Monthly
            >= 12 => ToolboxTalkFrequency.Annually,
            _ => ToolboxTalkFrequency.Monthly,         // 4–11 months: closest legacy bucket
        };
    }

    /// <summary>
    /// Convert the legacy Frequency enum to canonical refresher fields.
    /// Weekly has no months equivalent and was never functional for refresher scheduling
    /// (RefresherSchedulingService uses integer months). Maps to no-refresher, preserving
    /// the existing interval so a subsequent new-wizard Step 4 edit can restore it cleanly.
    /// </summary>
    public static (bool RequiresRefresher, int RefresherIntervalMonths) ToCanonicalFields(
        ToolboxTalkFrequency frequency,
        int existingIntervalMonths = 12)
    {
        return frequency switch
        {
            ToolboxTalkFrequency.Monthly => (true, 1),
            ToolboxTalkFrequency.Annually => (true, 12),
            // Weekly: no months equivalent; preserve existing interval so Step 4 values survive
            ToolboxTalkFrequency.Weekly => (false, existingIntervalMonths),
            _ => (false, existingIntervalMonths), // Once
        };
    }

    /// <summary>
    /// Convert the wizard's string RefresherFrequency value (used by Step 4 and tenant
    /// defaults) to canonical refresher fields.
    /// Accepted values: "Once", "Monthly", "Quarterly", "Annually". Unknown or null → Once.
    /// <paramref name="existingIntervalMonths"/> is preserved when the value is "Once" so
    /// that a user who switches back to a non-Once value later gets the same interval.
    /// </summary>
    public static (bool RequiresRefresher, int RefresherIntervalMonths) FromWizardFrequencyString(
        string? wizardFrequency,
        int existingIntervalMonths = 12)
    {
        return wizardFrequency switch
        {
            "Monthly" => (true, 1),
            "Quarterly" => (true, 3),
            "Annually" => (true, 12),
            _ => (false, existingIntervalMonths), // Once or unrecognised — preserve interval
        };
    }
}
