using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Domain;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Core.Infrastructure.Data;

namespace QuantumBuild.API.Controllers;

[ApiController]
[Route("api/dpa")]
[Authorize]
public class DpaController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DpaController(ApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var tenantId = _currentUserService.TenantId;

        var accepted = await _context.DpaAcceptances
            .AnyAsync(d => d.TenantId == tenantId && d.DpaVersion == DpaConstants.CurrentDpaVersion);

        return Ok(new { accepted, version = DpaConstants.CurrentDpaVersion });
    }

    [HttpPost("accept")]
    public async Task<IActionResult> Accept([FromBody] AcceptDpaRequest request)
    {
        var tenantId = _currentUserService.TenantId;
        var userId = _currentUserService.UserIdGuid;

        var alreadyAccepted = await _context.DpaAcceptances
            .AnyAsync(d => d.TenantId == tenantId && d.DpaVersion == DpaConstants.CurrentDpaVersion);

        if (alreadyAccepted)
            return Ok(new { accepted = true });

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var acceptance = new DpaAcceptance
        {
            TenantId = tenantId,
            AcceptedByUserId = userId,
            OrganisationLegalName = request.OrganisationLegalName,
            SignatoryFullName = request.SignatoryFullName,
            SignatoryRole = request.SignatoryRole,
            CompanyRegistrationNo = request.CompanyRegistrationNo,
            Country = request.Country,
            IpAddress = ipAddress,
            AcceptedAt = DateTime.UtcNow,
            DpaVersion = DpaConstants.CurrentDpaVersion,
        };

        _context.DpaAcceptances.Add(acceptance);
        await _context.SaveChangesAsync();

        return Ok(new { accepted = true });
    }
}

public record AcceptDpaRequest(
    string OrganisationLegalName,
    string SignatoryFullName,
    string SignatoryRole,
    string? CompanyRegistrationNo,
    string Country
);
