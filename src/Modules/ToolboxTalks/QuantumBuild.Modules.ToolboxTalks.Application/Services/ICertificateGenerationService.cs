using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Services;

/// <summary>
/// Service for generating PDF certificates for completed toolbox talks and courses
/// </summary>
public interface ICertificateGenerationService
{
    /// <summary>
    /// Generates a certificate for a completed standalone toolbox talk.
    /// Skips if the talk's GenerateCertificate is false or the talk is part of a course.
    /// </summary>
    Task<ToolboxTalkCertificate?> GenerateTalkCertificateAsync(
        ScheduledTalk completedTalk,
        string? signatureDataUrl,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a certificate for a completed course assignment.
    /// Skips if the course's GenerateCertificate is false.
    /// </summary>
    Task<ToolboxTalkCertificate?> GenerateCourseCertificateAsync(
        ToolboxTalkCourseAssignment completedAssignment,
        string? signatureDataUrl,
        CancellationToken ct = default);
}
