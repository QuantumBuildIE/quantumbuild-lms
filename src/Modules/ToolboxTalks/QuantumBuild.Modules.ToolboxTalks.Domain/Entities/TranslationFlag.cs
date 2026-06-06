using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

public class TranslationFlag : TenantEntity
{
    public Guid ToolboxTalkId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
    public FlagSeverity Severity { get; set; }
    public string Reason { get; set; } = string.Empty;

    public ToolboxTalk? ToolboxTalk { get; set; }
}
