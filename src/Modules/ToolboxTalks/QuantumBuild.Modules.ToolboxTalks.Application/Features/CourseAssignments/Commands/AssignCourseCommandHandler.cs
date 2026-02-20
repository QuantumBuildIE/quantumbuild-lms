using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.CourseAssignments.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.CourseAssignments.Commands;

public class AssignCourseCommandHandler : IRequestHandler<AssignCourseCommand, List<ToolboxTalkCourseAssignmentDto>>
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICoreDbContext _coreDbContext;
    private readonly IToolboxTalkEmailService _emailService;
    private readonly ILogger<AssignCourseCommandHandler> _logger;

    public AssignCourseCommandHandler(
        IToolboxTalksDbContext dbContext,
        ICoreDbContext coreDbContext,
        IToolboxTalkEmailService emailService,
        ILogger<AssignCourseCommandHandler> logger)
    {
        _dbContext = dbContext;
        _coreDbContext = coreDbContext;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<List<ToolboxTalkCourseAssignmentDto>> Handle(AssignCourseCommand request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId;
        var dto = request.Dto;

        // 1. Get course with items
        var course = await _dbContext.ToolboxTalkCourses
            .Include(c => c.CourseItems.Where(ci => !ci.IsDeleted))
                .ThenInclude(ci => ci.ToolboxTalk)
            .FirstOrDefaultAsync(c => c.Id == dto.CourseId && c.TenantId == tenantId && !c.IsDeleted, cancellationToken);

        if (course == null)
            throw new KeyNotFoundException("Course not found");

        if (!course.IsActive)
            throw new InvalidOperationException("Course is not active");

        var courseItems = course.CourseItems
            .Where(ci => ci.ToolboxTalk != null && !ci.ToolboxTalk.IsDeleted)
            .OrderBy(ci => ci.OrderIndex)
            .ToList();

        if (!courseItems.Any())
            throw new InvalidOperationException("Course has no talks");

        // 2. Validate employees exist
        var employeeIds = dto.Assignments.Select(a => a.EmployeeId).Distinct().ToList();

        var employees = await _coreDbContext.Employees
            .Where(e => employeeIds.Contains(e.Id) && e.TenantId == tenantId && !e.IsDeleted)
            .ToListAsync(cancellationToken);

        if (employees.Count != employeeIds.Count)
            throw new KeyNotFoundException("One or more employees not found");

        // 3. Check for existing active assignments - skip employees who already have one
        var existingEmployeeIds = await _dbContext.ToolboxTalkCourseAssignments
            .Where(a => a.CourseId == dto.CourseId
                && employeeIds.Contains(a.EmployeeId)
                && a.Status != CourseAssignmentStatus.Completed
                && !a.IsDeleted)
            .Select(a => a.EmployeeId)
            .ToListAsync(cancellationToken);

        var existingEmployeeIdSet = existingEmployeeIds.ToHashSet();
        var eligibleAssignments = dto.Assignments
            .Where(a => !existingEmployeeIdSet.Contains(a.EmployeeId))
            .ToList();

        if (!eligibleAssignments.Any())
            throw new InvalidOperationException("All selected employees already have active assignments for this course");

        // Build employee lookup
        var employeeLookup = employees.ToDictionary(e => e.Id);

        // 4. Create assignments
        var results = new List<ToolboxTalkCourseAssignmentDto>();

        foreach (var assignmentDto in eligibleAssignments)
        {
            var employee = employeeLookup[assignmentDto.EmployeeId];

            var assignment = new ToolboxTalkCourseAssignment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CourseId = course.Id,
                EmployeeId = employee.Id,
                AssignedAt = DateTime.UtcNow,
                DueDate = dto.DueDate.HasValue
                    ? DateTime.SpecifyKind(dto.DueDate.Value, DateTimeKind.Utc)
                    : null,
                Status = CourseAssignmentStatus.Assigned,
            };

            // Determine which talks to include
            var itemsToInclude = courseItems;
            if (assignmentDto.IncludedTalkIds != null && assignmentDto.IncludedTalkIds.Any())
            {
                var includedSet = assignmentDto.IncludedTalkIds.ToHashSet();
                itemsToInclude = courseItems
                    .Where(ci => includedSet.Contains(ci.ToolboxTalkId))
                    .ToList();
            }

            var scheduledTalkDtos = new List<CourseScheduledTalkDto>();

            foreach (var item in itemsToInclude)
            {
                var scheduledTalk = new ScheduledTalk
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ToolboxTalkId = item.ToolboxTalkId,
                    EmployeeId = employee.Id,
                    RequiredDate = DateTime.UtcNow,
                    DueDate = dto.DueDate.HasValue
                        ? DateTime.SpecifyKind(dto.DueDate.Value, DateTimeKind.Utc)
                        : DateTime.UtcNow.AddDays(30),
                    Status = ScheduledTalkStatus.Pending,
                    CourseAssignmentId = assignment.Id,
                    CourseOrderIndex = item.OrderIndex,
                };

                _dbContext.ScheduledTalks.Add(scheduledTalk);

                scheduledTalkDtos.Add(new CourseScheduledTalkDto
                {
                    ScheduledTalkId = scheduledTalk.Id,
                    ToolboxTalkId = item.ToolboxTalkId,
                    TalkCode = item.ToolboxTalk.Code,
                    TalkTitle = item.ToolboxTalk.Title,
                    OrderIndex = item.OrderIndex,
                    IsRequired = item.IsRequired,
                    Status = ScheduledTalkStatus.Pending.ToString(),
                    CompletedAt = null,
                    IsLocked = false,
                    LockedReason = null,
                });
            }

            _dbContext.ToolboxTalkCourseAssignments.Add(assignment);

            results.Add(new ToolboxTalkCourseAssignmentDto
            {
                Id = assignment.Id,
                CourseId = course.Id,
                CourseTitle = course.Title,
                CourseDescription = course.Description,
                EmployeeId = employee.Id,
                EmployeeName = $"{employee.FirstName} {employee.LastName}",
                EmployeeCode = employee.EmployeeCode,
                AssignedAt = assignment.AssignedAt,
                DueDate = assignment.DueDate,
                StartedAt = null,
                CompletedAt = null,
                Status = CourseAssignmentStatus.Assigned.ToString(),
                IsRefresher = false,
                RefresherDueDate = null,
                TotalTalks = itemsToInclude.Count,
                CompletedTalks = 0,
                ScheduledTalks = scheduledTalkDtos,
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Send email notifications to each assigned employee
        foreach (var assignmentDto in eligibleAssignments)
        {
            var employee = employeeLookup[assignmentDto.EmployeeId];

            // Determine talk count for this employee
            var talkCount = courseItems.Count;
            if (assignmentDto.IncludedTalkIds != null && assignmentDto.IncludedTalkIds.Any())
            {
                talkCount = courseItems.Count(ci => assignmentDto.IncludedTalkIds.Contains(ci.ToolboxTalkId));
            }

            try
            {
                await _emailService.SendCourseAssignmentEmailAsync(
                    course, employee, talkCount, dto.DueDate, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send course assignment email for Course {CourseId} to Employee {EmployeeId}",
                    course.Id, employee.Id);
                // Continue processing - don't fail the entire operation due to email failure
            }
        }

        return results;
    }
}
