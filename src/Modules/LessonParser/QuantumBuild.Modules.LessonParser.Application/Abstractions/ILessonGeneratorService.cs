namespace QuantumBuild.Modules.LessonParser.Application.Abstractions;

/// <summary>
/// Generates ToolboxTalk entities and a Course from extracted document content using AI
/// </summary>
public interface ILessonGeneratorService
{
    /// <summary>
    /// Calls Claude AI to break extracted content into topics, then creates
    /// ToolboxTalk entities (with sections and questions) and a ToolboxTalkCourse
    /// </summary>
    /// <param name="extractedContent">Text extracted from the source document</param>
    /// <param name="tenantId">Tenant to create entities under</param>
    /// <param name="createdBy">User ID to record as creator</param>
    /// <param name="userId">User ID for AI usage logging (null for system calls)</param>
    /// <param name="progress">Progress reporter for tracking generation stages</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the created course ID and talk count</returns>
    Task<LessonParseResult> GenerateFromContentAsync(
        ExtractionResult extractedContent,
        Guid tenantId,
        string createdBy,
        Guid? userId,
        IProgress<LessonParseProgress> progress,
        CancellationToken cancellationToken = default);
}
