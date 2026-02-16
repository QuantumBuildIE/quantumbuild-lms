using MediatR;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Certificates.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Certificates.Queries;

public record GetCertificateReportQuery : IRequest<CertificateReportDto>
{
    public Guid TenantId { get; init; }
    public string? Status { get; init; }
    public string? Type { get; init; }
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
