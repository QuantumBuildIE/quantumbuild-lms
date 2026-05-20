using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetCustomerUsageReport;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.API.Controllers;

/// <summary>
/// SuperUser-only monitoring endpoints. Not accessible to tenant users.
/// Route base: /api/admin/monitoring
/// </summary>
[ApiController]
[Route("api/admin/monitoring")]
[Authorize]
public class MonitoringController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ILogger<MonitoringController> _logger;

    public MonitoringController(
        IMediator mediator,
        ICurrentUserService currentUser,
        IToolboxTalksDbContext dbContext,
        ILogger<MonitoringController> logger)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Returns one usage row per tenant: sign-up date, employee count, total/new learnings,
    /// completions since comparisonDate, last login, and at-risk flag.
    /// comparisonDate defaults to the stored LastReviewedAt (or 30 days ago if never reviewed).
    /// </summary>
    [HttpGet("customer-usage")]
    [ProducesResponseType(typeof(CustomerUsageReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCustomerUsage(
        [FromQuery] DateTimeOffset? comparisonDate,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsSuperUser)
            return Forbid();

        try
        {
            var result = await _mediator.Send(
                new GetCustomerUsageReportQuery { ComparisonDate = comparisonDate },
                cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading customer usage report");
            return StatusCode(500, Result.Fail("Error loading customer usage report"));
        }
    }

    /// <summary>
    /// Records the current UTC time as LastReviewedAt on the singleton state row.
    /// Creates the row on first call.
    /// </summary>
    [HttpPost("customer-usage/mark-reviewed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MarkReviewed(CancellationToken cancellationToken)
    {
        if (!_currentUser.IsSuperUser)
            return Forbid();

        try
        {
            var now = DateTimeOffset.UtcNow;

            // Find or create the singleton state row
            var state = await _dbContext.CustomerUsageReportStates
                .IgnoreQueryFilters()
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (state is null)
            {
                state = new CustomerUsageReportState { LastReviewedAt = now };
                _dbContext.CustomerUsageReportStates.Add(state);
            }
            else
            {
                state.LastReviewedAt = now;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new { lastReviewedAt = now });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating customer usage report state");
            return StatusCode(500, Result.Fail("Error updating review timestamp"));
        }
    }
}
