namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Certificates.DTOs;

public class CertificateDto
{
    public Guid Id { get; set; }
    public string CertificateNumber { get; set; } = string.Empty;
    public string CertificateType { get; set; } = string.Empty;
    public string TrainingTitle { get; set; } = string.Empty;
    public List<string>? IncludedTalks { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
    public bool IsExpiringSoon => ExpiresAt.HasValue && !IsExpired && ExpiresAt.Value < DateTime.UtcNow.AddDays(30);
    public bool IsRefresher { get; set; }
}
