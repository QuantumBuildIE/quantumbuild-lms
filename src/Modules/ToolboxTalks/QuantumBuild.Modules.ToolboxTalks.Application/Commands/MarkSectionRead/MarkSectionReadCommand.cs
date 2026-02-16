using MediatR;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.MarkSectionRead;

/// <summary>
/// Command to mark a section as read by the current employee.
/// Used in the employee portal as they progress through a toolbox talk.
/// </summary>
public record MarkSectionReadCommand : IRequest<ScheduledTalkSectionProgressDto>
{
    /// <summary>
    /// The scheduled talk being completed
    /// </summary>
    public Guid ScheduledTalkId { get; init; }

    /// <summary>
    /// The section to mark as read
    /// </summary>
    public Guid SectionId { get; init; }

    /// <summary>
    /// Optional time spent on this section in seconds (for tracking purposes)
    /// </summary>
    public int? TimeSpentSeconds { get; init; }
}
