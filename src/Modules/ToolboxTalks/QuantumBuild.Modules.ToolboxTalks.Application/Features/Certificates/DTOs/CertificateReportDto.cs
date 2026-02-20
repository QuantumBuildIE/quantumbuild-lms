namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Certificates.DTOs;

public class CertificateReportDto
{
    public List<CertificateReportItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public int TotalCertificates { get; set; }
    public int ValidCertificates { get; set; }
    public int ExpiredCertificates { get; set; }
    public int ExpiringSoonCertificates { get; set; }
}

public class CertificateReportItemDto
{
    public Guid Id { get; set; }
    public string CertificateNumber { get; set; } = string.Empty;
    public string CertificateType { get; set; } = string.Empty;
    public string TrainingCode { get; set; } = string.Empty;
    public string TrainingTitle { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }
    public Guid EmployeeId { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
    public bool IsExpiringSoon => ExpiresAt.HasValue && !IsExpired && ExpiresAt.Value < DateTime.UtcNow.AddDays(30);
    public bool IsRefresher { get; set; }
}
