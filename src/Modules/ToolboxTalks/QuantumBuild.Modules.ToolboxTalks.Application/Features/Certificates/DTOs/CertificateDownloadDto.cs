namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Certificates.DTOs;

public class CertificateDownloadDto
{
    public string StoragePath { get; set; } = string.Empty;
    public string CertificateNumber { get; set; } = string.Empty;
    public string FileName => $"Certificate-{CertificateNumber}.pdf";
}
