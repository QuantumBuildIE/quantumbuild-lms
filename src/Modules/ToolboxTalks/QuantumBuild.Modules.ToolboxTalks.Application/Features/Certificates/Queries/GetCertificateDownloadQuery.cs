using MediatR;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Certificates.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Certificates.Queries;

public record GetCertificateDownloadQuery : IRequest<CertificateDownloadDto?>
{
    public Guid TenantId { get; init; }
    public Guid EmployeeId { get; init; }
    public Guid CertificateId { get; init; }
}
