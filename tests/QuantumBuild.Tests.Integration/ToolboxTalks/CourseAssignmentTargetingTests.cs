using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for targeting a COURSE assignment by Department and/or Site
/// (Course Targeting Chunk 2b). Verifies the expansion to member employees, via the shared
/// TargetEmployeeResolver, is exact: union of department + site + explicit employees,
/// deduplicated, active-only, tenant-scoped — and that it feeds the existing course-assignment
/// logic (already-assigned skip, all-talks default for expansion-derived employees) unchanged.
/// Course assignment is admin-only, so no supervisor scoping is exercised here (mirrors
/// ScheduleTargetingTests.cs for learning schedules).
/// </summary>
public class CourseAssignmentTargetingTests : IntegrationTestBase
{
    public CourseAssignmentTargetingTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task AssignCourse_TargetDepartment_AssignsExactlyActiveDepartmentMembers()
    {
        // Arrange
        var department = await CreateDepartmentAsync("Course Targeting Dept A");
        var memberA = await CreateEmployeeAsync("Member", "A", departmentId: department);
        var memberB = await CreateEmployeeAsync("Member", "B", departmentId: department);
        var inactiveMember = await CreateEmployeeAsync("Inactive", "Member", departmentId: department, isActive: false);
        var outsider = await CreateEmployeeAsync("Outsider", "Person");
        var course = await CreateCourseWithTalksAsync(talkCount: 2);

        var body = new
        {
            CourseId = course.CourseId,
            Assignments = Array.Empty<object>(),
            TargetDepartmentIds = new[] { department }
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/course-assignments", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<List<AssignmentResult>>>();
        result!.Success.Should().BeTrue();
        var assignedIds = result.Data!.Select(a => a.EmployeeId).ToHashSet();
        assignedIds.Should().BeEquivalentTo(new[] { memberA, memberB });
        assignedIds.Should().NotContain(inactiveMember);
        assignedIds.Should().NotContain(outsider);
        // Expansion-derived employees receive all course talks (IncludedTalkIds default)
        result.Data!.Should().OnlyContain(a => a.TotalTalks == course.TalkCount);
    }

    [Fact]
    public async Task AssignCourse_TargetSite_AssignsExactlySiteMembers()
    {
        // Arrange
        var site = await CreateSiteAsync("Course Targeting Site A");
        var memberA = await CreateEmployeeAsync("Site", "A", siteId: site);
        var memberB = await CreateEmployeeAsync("Site", "B", siteId: site);
        var outsider = await CreateEmployeeAsync("Off", "Site");
        var course = await CreateCourseWithTalksAsync(talkCount: 1);

        var body = new
        {
            CourseId = course.CourseId,
            Assignments = Array.Empty<object>(),
            TargetSiteIds = new[] { site }
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/course-assignments", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<List<AssignmentResult>>>();
        var assignedIds = result!.Data!.Select(a => a.EmployeeId).ToHashSet();
        assignedIds.Should().BeEquivalentTo(new[] { memberA, memberB });
        assignedIds.Should().NotContain(outsider);
    }

    [Fact]
    public async Task AssignCourse_DepartmentAndSiteTargets_UnionDeduplicated()
    {
        // Arrange
        var department = await CreateDepartmentAsync("Course Union Dept");
        var site = await CreateSiteAsync("Course Union Site");
        var deptOnly = await CreateEmployeeAsync("Dept", "Only", departmentId: department);
        var siteOnly = await CreateEmployeeAsync("Site", "Only", siteId: site);
        // In both the targeted department AND the targeted site — must appear exactly once
        var both = await CreateEmployeeAsync("Both", "Targets", departmentId: department, siteId: site);
        var course = await CreateCourseWithTalksAsync(talkCount: 1);

        var body = new
        {
            CourseId = course.CourseId,
            Assignments = Array.Empty<object>(),
            TargetDepartmentIds = new[] { department },
            TargetSiteIds = new[] { site }
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/course-assignments", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<List<AssignmentResult>>>();
        result!.Data.Should().HaveCount(3);
        var assignedIds = result.Data!.Select(a => a.EmployeeId).ToHashSet();
        assignedIds.Should().BeEquivalentTo(new[] { deptOnly, siteOnly, both });
    }

    [Fact]
    public async Task AssignCourse_ExplicitEmployeePlusDepartmentTarget_UnionDeduplicated_ExpandedGetsAllTalks()
    {
        // Arrange
        var department = await CreateDepartmentAsync("Course Explicit Union Dept");
        var deptMember = await CreateEmployeeAsync("Dept", "Member", departmentId: department);
        var explicitEmployee = await CreateEmployeeAsync("Explicit", "Pick");
        var course = await CreateCourseWithTalksAsync(talkCount: 3);

        var body = new
        {
            CourseId = course.CourseId,
            // deptMember reachable only via the department target; explicitEmployee picked with a
            // customized (narrowed) talk list to prove explicit customization still works
            Assignments = new[]
            {
                new { EmployeeId = explicitEmployee, IncludedTalkIds = new[] { course.TalkIds[0] } }
            },
            TargetDepartmentIds = new[] { department }
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/course-assignments", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<List<AssignmentResult>>>();
        result!.Data.Should().HaveCount(2);
        var assignedIds = result.Data!.Select(a => a.EmployeeId).ToHashSet();
        assignedIds.Should().BeEquivalentTo(new[] { deptMember, explicitEmployee });

        var explicitAssignment = result.Data!.Single(a => a.EmployeeId == explicitEmployee);
        explicitAssignment.TotalTalks.Should().Be(1);

        var expandedAssignment = result.Data!.Single(a => a.EmployeeId == deptMember);
        expandedAssignment.TotalTalks.Should().Be(course.TalkCount);
    }

    [Fact]
    public async Task AssignCourse_ExpandedEmployeeAlreadyAssigned_IsSkipped()
    {
        // Arrange — an employee reachable via the department target who already has a
        // non-Completed assignment for this course must be skipped, not double-assigned,
        // exactly like the pre-existing explicit-list skip logic (AssignCourseCommandHandler.cs).
        var department = await CreateDepartmentAsync("Course Already Assigned Dept");
        var alreadyAssigned = await CreateEmployeeAsync("Already", "Assigned", departmentId: department);
        var freshMember = await CreateEmployeeAsync("Fresh", "Member", departmentId: department);
        var course = await CreateCourseWithTalksAsync(talkCount: 1);

        var firstAssignBody = new
        {
            CourseId = course.CourseId,
            Assignments = new[] { new { EmployeeId = alreadyAssigned, IncludedTalkIds = (Guid[]?)null } }
        };
        var firstResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/course-assignments", firstAssignBody);
        firstResponse.EnsureSuccessStatusCode();

        var targetBody = new
        {
            CourseId = course.CourseId,
            Assignments = Array.Empty<object>(),
            TargetDepartmentIds = new[] { department }
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/course-assignments", targetBody);

        // Assert — only the fresh member gets a new assignment; the already-assigned employee is skipped
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<List<AssignmentResult>>>();
        result!.Data.Should().ContainSingle();
        result.Data!.Single().EmployeeId.Should().Be(freshMember);

        // Verify no duplicate assignment row was created for the already-assigned employee
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var assignmentCount = await db.Set<ToolboxTalkCourseAssignment>()
            .IgnoreQueryFilters()
            .CountAsync(a => a.CourseId == course.CourseId && a.EmployeeId == alreadyAssigned && !a.IsDeleted);
        assignmentCount.Should().Be(1);
    }

    [Fact]
    public async Task AssignCourse_DepartmentFromAnotherTenant_ReturnsBadRequest()
    {
        // Arrange — a department GUID that doesn't belong to the caller's tenant must be rejected
        var foreignDepartmentId = Guid.NewGuid();
        var course = await CreateCourseWithTalksAsync(talkCount: 1);

        var body = new
        {
            CourseId = course.CourseId,
            Assignments = Array.Empty<object>(),
            TargetDepartmentIds = new[] { foreignDepartmentId }
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/course-assignments", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AssignCourse_SiteFromAnotherTenant_ReturnsBadRequest()
    {
        // Arrange — a site GUID that doesn't belong to the caller's tenant must be rejected
        var foreignSiteId = Guid.NewGuid();
        var course = await CreateCourseWithTalksAsync(talkCount: 1);

        var body = new
        {
            CourseId = course.CourseId,
            Assignments = Array.Empty<object>(),
            TargetSiteIds = new[] { foreignSiteId }
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/course-assignments", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AssignCourse_NoEmployeesAndNoTargets_ReturnsBadRequest()
    {
        // Arrange — neither explicit employees nor department/site targets supplied
        var course = await CreateCourseWithTalksAsync(talkCount: 1);

        var body = new
        {
            CourseId = course.CourseId,
            Assignments = Array.Empty<object>()
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/course-assignments", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #region Helper Methods

    private async Task<Guid> CreateDepartmentAsync(string name)
    {
        var command = new { Name = $"{name} {Guid.NewGuid():N}", Code = (string?)null, IsActive = true };
        var response = await AdminClient.PostAsJsonAsync("/api/departments", command);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<IdOnly>>();
        return result!.Data!.Id;
    }

    private async Task<Guid> CreateSiteAsync(string name)
    {
        var command = new { SiteName = $"{name} {Guid.NewGuid():N}", IsActive = true };
        var response = await AdminClient.PostAsJsonAsync("/api/sites", command);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<IdOnly>>();
        return result!.Data!.Id;
    }

    private async Task<Guid> CreateEmployeeAsync(
        string firstName,
        string lastName,
        Guid? departmentId = null,
        Guid? siteId = null,
        bool isActive = true)
    {
        var command = new
        {
            FirstName = firstName,
            LastName = lastName,
            DepartmentId = departmentId,
            PrimarySiteId = siteId,
            IsActive = isActive,
            CreateUserAccount = false
        };
        var response = await AdminClient.PostAsJsonAsync("/api/employees", command);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<IdOnly>>();
        return result!.Data!.Id;
    }

    /// <summary>Creates a Draft talk with one section via the CRUD endpoint.</summary>
    private async Task<Guid> CreateDraftTalkAsync(string title)
    {
        var body = new
        {
            Title = title,
            Frequency = "Once",
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section 1", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };

        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", body);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<IdOnly>();
        return dto!.Id;
    }

    /// <summary>Flips a Draft talk to Published directly via DbContext — the CRUD endpoint doesn't
    /// expose Status and driving the full publish workflow is out of scope for these tests
    /// (mirrors CourseComposeExistingHappyPathTests.CreatePublishedTalkAsync).</summary>
    private async Task<Guid> CreatePublishedTalkAsync(string title)
    {
        var talkId = await CreateDraftTalkAsync(title);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var talk = await db.Set<Modules.ToolboxTalks.Domain.Entities.ToolboxTalk>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == talkId && !t.IsDeleted);
        talk!.Status = ToolboxTalkStatus.Published;
        await db.SaveChangesAsync();
        return talkId;
    }

    private async Task<CourseInfo> CreateCourseWithTalksAsync(int talkCount)
    {
        var talkIds = new List<Guid>();
        for (var i = 0; i < talkCount; i++)
        {
            talkIds.Add(await CreatePublishedTalkAsync($"Course Targeting Talk {i} {Guid.NewGuid():N}"));
        }

        var courseBody = new
        {
            Title = $"Course Targeting Course {Guid.NewGuid():N}",
            Description = (string?)null,
            IsActive = true,
            RequireSequentialCompletion = false,
            RequiresRefresher = false,
            RefresherIntervalMonths = 12,
            GenerateCertificate = false,
            AutoAssignToNewEmployees = false,
            AutoAssignDueDays = 14,
            Items = talkIds.Select((id, idx) => new { ToolboxTalkId = id, OrderIndex = idx, IsRequired = true }).ToArray()
        };

        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/courses", courseBody);
        response.EnsureSuccessStatusCode();
        var course = await response.Content.ReadFromJsonAsync<CourseDto>();
        return new CourseInfo(course!.Id, talkIds, talkCount);
    }

    #endregion

    #region Response DTOs

    private record ResultWrapper<T>(bool Success, T? Data, string? Message, List<string>? Errors);

    private record IdOnly(Guid Id);

    private record CourseDto(Guid Id, string Title, bool IsActive, int TalkCount);

    private record AssignmentResult(Guid Id, Guid CourseId, Guid EmployeeId, int TotalTalks);

    private record CourseInfo(Guid CourseId, List<Guid> TalkIds, int TalkCount);

    #endregion
}
