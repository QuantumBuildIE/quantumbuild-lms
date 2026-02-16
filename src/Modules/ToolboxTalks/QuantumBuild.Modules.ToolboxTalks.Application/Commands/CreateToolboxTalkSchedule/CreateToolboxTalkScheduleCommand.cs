using MediatR;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.CreateToolboxTalkSchedule;

/// <summary>
/// Command to create a new toolbox talk schedule
/// </summary>
public record CreateToolboxTalkScheduleCommand : IRequest<ToolboxTalkScheduleDto>
{
    /// <summary>
    /// Tenant identifier
    /// </summary>
    public Guid TenantId { get; init; }

    /// <summary>
    /// The toolbox talk to schedule
    /// </summary>
    public Guid ToolboxTalkId { get; init; }

    /// <summary>
    /// Date when the talk should be assigned
    /// </summary>
    public DateTime ScheduledDate { get; init; }

    /// <summary>
    /// End date for recurring schedules (null for one-time)
    /// </summary>
    public DateTime? EndDate { get; init; }

    /// <summary>
    /// Frequency at which the schedule recurs
    /// </summary>
    public ToolboxTalkFrequency Frequency { get; init; } = ToolboxTalkFrequency.Once;

    /// <summary>
    /// If true, assigns to all active employees
    /// </summary>
    public bool AssignToAllEmployees { get; init; } = false;

    /// <summary>
    /// Specific employee IDs to assign (when not assigning to all)
    /// </summary>
    public List<Guid> EmployeeIds { get; init; } = new();

    /// <summary>
    /// Additional notes about the schedule
    /// </summary>
    public string? Notes { get; init; }
}
