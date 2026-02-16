using QuantumBuild.Core.Application.Features.Sites.DTOs;
using QuantumBuild.Core.Application.Models;

namespace QuantumBuild.Core.Application.Features.Sites;

public interface ISiteService
{
    Task<Result<List<SiteDto>>> GetAllAsync();
    Task<Result<PaginatedList<SiteDto>>> GetPaginatedAsync(GetSitesQueryDto query);
    Task<Result<SiteDto>> GetByIdAsync(Guid id);
    Task<Result<SiteDto>> CreateAsync(CreateSiteDto dto);
    Task<Result<SiteDto>> UpdateAsync(Guid id, UpdateSiteDto dto);
    Task<Result> DeleteAsync(Guid id);
}
