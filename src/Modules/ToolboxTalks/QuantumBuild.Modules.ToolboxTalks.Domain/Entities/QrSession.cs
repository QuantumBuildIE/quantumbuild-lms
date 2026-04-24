using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

public class QrSession : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public Guid QrCodeId { get; set; }
    public QrCode QrCode { get; set; } = null!;

    public Guid SessionToken { get; set; }
    public string Language { get; set; } = "en";
    public ContentMode ContentMode { get; set; } = ContentMode.Training;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? SignedOffAt { get; set; }
    public int? Score { get; set; }
    public QrSessionStatus Status { get; set; } = QrSessionStatus.Active;
}
