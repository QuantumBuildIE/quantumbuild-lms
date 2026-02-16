using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Certificates.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Certificates.Queries;

public class GetCertificateReportQueryHandler : IRequestHandler<GetCertificateReportQuery, CertificateReportDto>
{
    private readonly IToolboxTalksDbContext _dbContext;

    public GetCertificateReportQueryHandler(IToolboxTalksDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CertificateReportDto> Handle(GetCertificateReportQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var expiringSoonThreshold = now.AddDays(30);

        var query = _dbContext.ToolboxTalkCertificates
            .Where(c => c.TenantId == request.TenantId && !c.IsDeleted);

        // Filter by type
        if (!string.IsNullOrEmpty(request.Type))
        {
            if (Enum.TryParse<CertificateType>(request.Type, true, out var certType))
            {
                query = query.Where(c => c.CertificateType == certType);
            }
        }

        // Filter by search (employee name, training title, employee code)
        if (!string.IsNullOrEmpty(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(c =>
                c.EmployeeName.ToLower().Contains(search) ||
                c.TrainingTitle.ToLower().Contains(search) ||
                (c.EmployeeCode != null && c.EmployeeCode.ToLower().Contains(search)));
        }

        // Calculate stats before status filter (across all matching type/search filters)
        var totalCertificates = await query.CountAsync(cancellationToken);
        var expiredCount = await query.CountAsync(c => c.ExpiresAt.HasValue && c.ExpiresAt.Value < now, cancellationToken);
        var expiringSoonCount = await query.CountAsync(c =>
            c.ExpiresAt.HasValue && c.ExpiresAt.Value >= now && c.ExpiresAt.Value < expiringSoonThreshold, cancellationToken);
        var validCount = totalCertificates - expiredCount;

        // Filter by status
        if (!string.IsNullOrEmpty(request.Status))
        {
            query = request.Status.ToLower() switch
            {
                "valid" => query.Where(c => !c.ExpiresAt.HasValue || c.ExpiresAt.Value >= now),
                "expired" => query.Where(c => c.ExpiresAt.HasValue && c.ExpiresAt.Value < now),
                "expiring" => query.Where(c => c.ExpiresAt.HasValue && c.ExpiresAt.Value >= now && c.ExpiresAt.Value < expiringSoonThreshold),
                _ => query
            };
        }

        var filteredCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.IssuedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CertificateReportItemDto
            {
                Id = c.Id,
                CertificateNumber = c.CertificateNumber,
                CertificateType = c.CertificateType.ToString(),
                TrainingTitle = c.TrainingTitle,
                EmployeeName = c.EmployeeName,
                EmployeeCode = c.EmployeeCode,
                EmployeeId = c.EmployeeId,
                IssuedAt = c.IssuedAt,
                ExpiresAt = c.ExpiresAt,
                IsRefresher = c.IsRefresher,
            })
            .ToListAsync(cancellationToken);

        return new CertificateReportDto
        {
            Items = items,
            TotalCount = filteredCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCertificates = totalCertificates,
            ValidCertificates = validCount,
            ExpiredCertificates = expiredCount,
            ExpiringSoonCertificates = expiringSoonCount,
        };
    }
}
