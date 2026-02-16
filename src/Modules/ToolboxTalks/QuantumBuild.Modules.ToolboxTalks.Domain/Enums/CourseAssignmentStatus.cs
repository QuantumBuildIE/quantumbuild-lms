namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// Status of a course assignment to an employee
/// </summary>
public enum CourseAssignmentStatus
{
    /// <summary>
    /// Course has been assigned but not started
    /// </summary>
    Assigned = 0,

    /// <summary>
    /// Employee has started at least one talk in the course
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// All required talks in the course have been completed
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Course has passed its due date without completion
    /// </summary>
    Overdue = 3
}
