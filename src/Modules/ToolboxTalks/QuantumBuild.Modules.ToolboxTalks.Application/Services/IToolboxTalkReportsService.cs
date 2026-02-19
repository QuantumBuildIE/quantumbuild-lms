using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Reports;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Services;

/// <summary>
/// Service for generating toolbox talk compliance reports
/// </summary>
public interface IToolboxTalkReportsService
{
    /// <summary>
    /// Get comprehensive compliance report with breakdowns by department and talk
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="dateFrom">Optional start date filter</param>
    /// <param name="dateTo">Optional end date filter</param>
    /// <param name="siteId">Optional site/department filter</param>
    /// <returns>Compliance report with metrics and breakdowns</returns>
    Task<ComplianceReportDto> GetComplianceReportAsync(
        Guid tenantId,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        Guid? siteId = null,
        List<Guid>? employeeIds = null);

    /// <summary>
    /// Get list of overdue toolbox talk assignments
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="siteId">Optional site/department filter</param>
    /// <param name="toolboxTalkId">Optional toolbox talk filter</param>
    /// <returns>List of overdue items</returns>
    Task<List<OverdueItemDto>> GetOverdueReportAsync(
        Guid tenantId,
        Guid? siteId = null,
        Guid? toolboxTalkId = null,
        List<Guid>? employeeIds = null);

    /// <summary>
    /// Get detailed completion records with pagination
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="dateFrom">Optional start date filter</param>
    /// <param name="dateTo">Optional end date filter</param>
    /// <param name="toolboxTalkId">Optional toolbox talk filter</param>
    /// <param name="siteId">Optional site/department filter</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Page size</param>
    /// <returns>Paginated list of completion details</returns>
    Task<PaginatedList<CompletionDetailDto>> GetCompletionReportAsync(
        Guid tenantId,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        Guid? toolboxTalkId = null,
        Guid? siteId = null,
        int pageNumber = 1,
        int pageSize = 20,
        List<Guid>? employeeIds = null);
}
