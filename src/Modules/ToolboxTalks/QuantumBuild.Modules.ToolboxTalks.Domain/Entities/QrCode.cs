using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

public class QrCode : TenantEntity
{
    public Guid QrLocationId { get; set; }
    public QrLocation QrLocation { get; set; } = null!;

    public Guid? ToolboxTalkId { get; set; }
    public ToolboxTalk? ToolboxTalk { get; set; }

    public string Name { get; set; } = string.Empty;
    public ContentMode ContentMode { get; set; } = ContentMode.Training;
    public string CodeToken { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? QrImageUrl { get; set; }
}
