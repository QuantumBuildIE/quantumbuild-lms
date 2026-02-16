namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// Type of certificate issued for training completion
/// </summary>
public enum CertificateType
{
    /// <summary>
    /// Certificate for completing an individual toolbox talk
    /// </summary>
    Talk = 0,

    /// <summary>
    /// Certificate for completing an entire course
    /// </summary>
    Course = 1
}
