using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Certificates.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Certificates.Queries;

public class GetMyCertificatesQueryHandler : IRequestHandler<GetMyCertificatesQuery, List<CertificateDto>>
{
    private readonly IToolboxTalksDbContext _dbContext;

    public GetMyCertificatesQueryHandler(IToolboxTalksDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<CertificateDto>> Handle(GetMyCertificatesQuery request, CancellationToken cancellationToken)
    {
        var rawCertificates = await _dbContext.ToolboxTalkCertificates
            .Where(c => c.TenantId == request.TenantId
                && c.EmployeeId == request.EmployeeId
                && !c.IsDeleted)
            .OrderByDescending(c => c.IssuedAt)
            .Select(c => new
            {
                c.Id,
                c.CertificateNumber,
                CertificateType = c.CertificateType.ToString(),
                TrainingCode = c.ToolboxTalk != null ? c.ToolboxTalk.Code : string.Empty,
                c.TrainingTitle,
                c.IncludedTalksJson,
                c.IssuedAt,
                c.ExpiresAt,
                c.IsRefresher,
            })
            .ToListAsync(cancellationToken);

        return rawCertificates.Select(c => new CertificateDto
        {
            Id = c.Id,
            CertificateNumber = c.CertificateNumber,
            CertificateType = c.CertificateType,
            TrainingCode = c.TrainingCode,
            TrainingTitle = c.TrainingTitle,
            IncludedTalks = c.IncludedTalksJson != null
                ? JsonSerializer.Deserialize<List<string>>(c.IncludedTalksJson)
                : null,
            IssuedAt = c.IssuedAt,
            ExpiresAt = c.ExpiresAt,
            IsRefresher = c.IsRefresher,
        }).ToList();
    }
}
