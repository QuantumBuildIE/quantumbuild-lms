using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Tests.Common.TestTenant;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Extended happy-path coverage for the compose-existing course flow (course-in-new-wizard,
/// Chunk 4): create course from published talks → add talks → (no separate publish step exists
/// — see deviation note below) → assign to an operator → operator sees the assignment and its
/// items. Locks the new-path behaviour against regression; does not cover the legacy wizard.
/// </summary>
[Collection("Integration")]
public class CourseComposeExistingHappyPathTests : IntegrationTestBase
{
    public CourseComposeExistingHappyPathTests(CustomWebApplicationFactory factory) : base(factory) { }

    private static string UniqueTitle(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..40];

    /// <summary>Creates a talk with one section via the CRUD endpoint. New talks default to Draft.</summary>
    private async Task<Guid> CreateDraftTalkAsync(string? title = null)
    {
        var body = new
        {
            Title = title ?? UniqueTitle("Compose Talk"),
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
        var dto = await response.Content.ReadFromJsonAsync<TalkIdDto>();
        return dto!.Id;
    }

    /// <summary>Creates a Published talk by creating a Draft then flipping its Status directly via
    /// DbContext, mirroring CourseCompositionTests.CreatePublishedTalkAsync — the CRUD endpoint doesn't
    /// expose Status and driving the full publish workflow is out of scope for this test.</summary>
    private async Task<Guid> CreatePublishedTalkAsync(string? title = null)
    {
        var talkId = await CreateDraftTalkAsync(title);
        await SetTalkStatusAsync(talkId, ToolboxTalkStatus.Published);
        return talkId;
    }

    private async Task SetTalkStatusAsync(Guid talkId, ToolboxTalkStatus status)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var talk = await db.Set<ToolboxTalk>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == talkId && !t.IsDeleted);
        talk!.Status = status;
        await db.SaveChangesAsync();
    }

    private static object CreateCourseBody(string title, params Guid[] talkIds) => new
    {
        Title = title,
        Description = (string?)null,
        IsActive = true,
        RequireSequentialCompletion = true,
        RequiresRefresher = false,
        RefresherIntervalMonths = 12,
        GenerateCertificate = false,
        AutoAssignToNewEmployees = false,
        AutoAssignDueDays = 14,
        Items = talkIds.Select((id, idx) => new { ToolboxTalkId = id, OrderIndex = idx, IsRequired = true }).ToArray()
    };

    [Fact]
    public async Task Course_ComposeExistingHappyPath_CreateAddPublishAssignVerify()
    {
        // ---- Seed: 3 published talks + 1 employee (operator is already seeded by TestTenantSeeder) ----
        var talkId1 = await CreatePublishedTalkAsync(UniqueTitle("Compose Talk A"));
        var talkId2 = await CreatePublishedTalkAsync(UniqueTitle("Compose Talk B"));
        var talkId3 = await CreatePublishedTalkAsync(UniqueTitle("Compose Talk C"));

        // ---- Compose: create the course from the 3 published talks ----
        var courseTitle = UniqueTitle("Compose Existing Happy Path");
        var createBody = CreateCourseBody(courseTitle, talkId1, talkId2, talkId3);

        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/courses", createBody);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var course = await createResponse.Content.ReadFromJsonAsync<CourseDto>();
        course.Should().NotBeNull();
        course!.Id.Should().NotBeEmpty();
        course.Title.Should().Be(courseTitle);
        course.TalkCount.Should().Be(3);
        course.Items.Should().HaveCount(3);
        course.Items.OrderBy(i => i.OrderIndex).Select(i => i.ToolboxTalkId)
            .Should().ContainInOrder(talkId1, talkId2, talkId3);

        // ---- Publish: ToolboxTalkCourse has no Status field and no separate publish endpoint — publish
        // readiness is enforced entirely at composition time (the Draft-talk guard already covered by
        // CourseCompositionTests). The create response's IsActive == true stands in for "published" here.
        course.IsActive.Should().BeTrue();

        // ---- Assign: assign the course to the seeded operator employee ----
        var assignBody = new
        {
            CourseId = course.Id,
            Assignments = new[]
            {
                new { EmployeeId = TestTenantConstants.Employees.OperatorEmployee, IncludedTalkIds = (Guid[]?)null }
            }
        };

        var assignResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/course-assignments", assignBody);

        assignResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var assignResult = await assignResponse.Content.ReadFromJsonAsync<ResultWrapper<List<ToolboxTalkCourseAssignmentDto>>>();
        assignResult.Should().NotBeNull();
        assignResult!.Success.Should().BeTrue();
        assignResult.Data.Should().NotBeNull();
        var createdAssignment = assignResult.Data!.Should().ContainSingle().Subject;
        createdAssignment.CourseId.Should().Be(course.Id);
        createdAssignment.EmployeeId.Should().Be(TestTenantConstants.Employees.OperatorEmployee);
        createdAssignment.TotalTalks.Should().Be(3);
        createdAssignment.ScheduledTalks.Should().HaveCount(3);
        createdAssignment.ScheduledTalks.Should().OnlyContain(st => st.Status == "Pending");
        createdAssignment.ScheduledTalks.OrderBy(st => st.OrderIndex).Select(st => st.ToolboxTalkId)
            .Should().ContainInOrder(talkId1, talkId2, talkId3);

        // ---- Verify: the operator sees the assigned course via the employee portal ----
        var myCoursesResponse = await OperatorClient.GetAsync("/api/my/toolbox-talks/courses");

        myCoursesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var myCoursesResult = await myCoursesResponse.Content.ReadFromJsonAsync<ResultWrapper<List<ToolboxTalkCourseAssignmentDto>>>();
        myCoursesResult.Should().NotBeNull();
        myCoursesResult!.Data.Should().NotBeNull();
        var myAssignment = myCoursesResult.Data!.SingleOrDefault(a => a.CourseId == course.Id);
        myAssignment.Should().NotBeNull("the operator should see the newly assigned course");
        myAssignment!.Status.Should().Be("Assigned");
        myAssignment.ScheduledTalks.Should().HaveCount(3);
        myAssignment.ScheduledTalks.Should().OnlyContain(st => st.Status == "Pending");
        myAssignment.ScheduledTalks.OrderBy(st => st.OrderIndex).Select(st => st.ToolboxTalkId)
            .Should().ContainInOrder(talkId1, talkId2, talkId3);
    }

    private record TalkIdDto(Guid Id);

    private record CourseDto(Guid Id, string Title, bool IsActive, int TalkCount, List<CourseItemDto> Items);

    private record CourseItemDto(Guid Id, Guid ToolboxTalkId, int OrderIndex, bool IsRequired);

    private record ToolboxTalkCourseAssignmentDto(
        Guid Id,
        Guid CourseId,
        string CourseTitle,
        Guid EmployeeId,
        string EmployeeName,
        string Status,
        int TotalTalks,
        int CompletedTalks,
        List<CourseScheduledTalkDto> ScheduledTalks
    );

    private record CourseScheduledTalkDto(
        Guid ScheduledTalkId,
        Guid ToolboxTalkId,
        string TalkTitle,
        int OrderIndex,
        bool IsRequired,
        string Status
    );

    private record ResultWrapper<T>(
        bool Success,
        T? Data,
        string? Message,
        List<string>? Errors
    );
}
