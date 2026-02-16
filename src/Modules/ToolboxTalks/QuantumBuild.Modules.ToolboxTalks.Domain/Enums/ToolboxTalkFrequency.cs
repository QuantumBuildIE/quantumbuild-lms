using System.Text.Json.Serialization;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// Frequency at which a toolbox talk must be completed
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ToolboxTalkFrequency
{
    /// <summary>
    /// One-time completion required
    /// </summary>
    Once = 1,

    /// <summary>
    /// Must be completed weekly
    /// </summary>
    Weekly = 2,

    /// <summary>
    /// Must be completed monthly
    /// </summary>
    Monthly = 3,

    /// <summary>
    /// Must be completed annually
    /// </summary>
    Annually = 4
}
