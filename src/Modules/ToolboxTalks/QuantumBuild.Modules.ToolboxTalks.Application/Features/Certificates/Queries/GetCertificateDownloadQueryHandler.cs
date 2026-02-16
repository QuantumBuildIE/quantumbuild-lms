using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Certificates.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Certificates.Queries;

public class GetCertificateDownloadQueryHandler : IRequestHandler<GetCertificateDownloadQuery, CertificateDownloadDto?>
{
    private readonly IToolboxTalksDbContext _dbContext;

    public GetCertificateDownloadQueryHandler(IToolboxTalksDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CertificateDownloadDto?> Handle(GetCertificateDownloadQuery request, CancellationToken cancellationToken)
    {
        var certificate = await _dbContext.ToolboxTalkCertificates
            .Where(c => c.Id == request.CertificateId
                && c.TenantId == request.TenantId
                && c.EmployeeId == request.EmployeeId
                && !c.IsDeleted)
            .Select(c => new CertificateDownloadDto
            {
                StoragePath = c.PdfStoragePath,
                CertificateNumber = c.CertificateNumber,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return certificate;
    }
}
