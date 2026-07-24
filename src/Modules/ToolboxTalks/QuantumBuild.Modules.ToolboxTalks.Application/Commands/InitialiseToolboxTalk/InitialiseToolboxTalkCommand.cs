using MediatR;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.InitialiseToolboxTalk;

/// <summary>
/// Creates a minimal Draft ToolboxTalk from the new learning-wizard Step 1.
/// No sections or questions are created here — those come from Step 2+.
/// </summary>
public record InitialiseToolboxTalkCommand : IRequest<ToolboxTalkDto>
{
    public Guid TenantId { get; init; }

    // Basic metadata
    public string Title { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }

    // Source / input
    public InputMode InputMode { get; init; } = InputMode.Text;
    public string SourceLanguageCode { get; init; } = "en";
    public string? SourceText { get; init; }
    public string? SourceFileUrl { get; init; }
    public string? SourceFileName { get; init; }
    public string? SourceFileType { get; init; }

    // Video URL mode
    public string? VideoUrl { get; init; }
    public VideoSource VideoSource { get; init; } = VideoSource.None;

    // Target languages
    public List<string> TargetLanguageCodes { get; init; } = new();

    // Audit metadata
    public string? ReviewerName { get; init; }
    public string? ReviewerOrg { get; init; }
    public string? ReviewerRole { get; init; }
    public string? DocumentRef { get; init; }
    public string? ClientName { get; init; }
    public string? AuditPurpose { get; init; }

    // Generation preferences
    public string AudienceRole { get; init; } = "Operator";

    // Nullable: an explicit value always wins over the tenant default (InitialiseToolboxTalkCommandHandler
    // reads request.X ?? tenantSettings?.DefaultX ?? initial-default). Null means "caller did not specify".
    public bool? PreserveSourceWording { get; init; }
    public bool? IncludeQuiz { get; init; }
}
