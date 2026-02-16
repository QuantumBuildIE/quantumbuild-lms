using System.Text.Json.Serialization;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// Status of a toolbox talk schedule
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ToolboxTalkScheduleStatus
{
    /// <summary>
    /// Schedule is in draft state and not yet active
    /// </summary>
    Draft = 1,

    /// <summary>
    /// Schedule is active and will create assignments
    /// </summary>
    Active = 2,

    /// <summary>
    /// Schedule has been completed (one-time schedules)
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Schedule has been cancelled
    /// </summary>
    Cancelled = 4
}
