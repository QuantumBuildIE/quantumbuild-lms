using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Tests.Common.TestTenant;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for the certificate completion email wiring (talk, course and admin
/// regenerate call sites) and the CertificateGenerationFailed / CertificateEmailFailed
/// diagnostic flags. Uses FakeToolboxTalkEmailService to observe/force email outcomes and
/// FakeR2StorageService.ShouldThrowOnUploadCertificate to force a genuine certificate
/// generation exception (as opposed to the clean-null business-rule path).
/// </summary>
[Collection("Integration")]
public class CertificateEmailTests : IntegrationTestBase
{
    public CertificateEmailTests(CustomWebApplicationFactory factory) : base(factory) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        FakeToolboxTalkEmailService.Reset();
        FakeR2StorageService.Reset();

        // Pre-existing test-seeding gap (unrelated to this chunk): TestTenantSeeder never wires
        // Employees.OperatorEmployee.UserId to Users.Operator.Id, so any handler that resolves the
        // current employee by UserId (e.g. MarkSectionReadCommandHandler, CompleteToolboxTalkCommandHandler)
        // fails with "No employee record found for the current user" for the Operator test client.
        // Wired here, scoped to this test file only, so the certificate/email flow can be exercised
        // end-to-end over real HTTP without touching the shared seeder.
        await LinkOperatorEmployeeToUserAsync();
    }

    private async Task LinkOperatorEmployeeToUserAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var employee = await db.Set<Employee>().IgnoreQueryFilters()
            .FirstAsync(e => e.Id == TestTenantConstants.Employees.OperatorEmployee);
        employee.UserId = TestTenantConstants.Users.Operator.Id;
        await db.SaveChangesAsync();
    }

    private static string UniqueTitle(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..40];

    #region Helpers

    private async Task<Guid> CreateTalkAsync(string? title = null, bool generateCertificate = true)
    {
        var body = new
        {
            Title = title ?? UniqueTitle("Cert Email Talk"),
            Frequency = "Once",
            RequiresQuiz = false,
            IsActive = true,
            GenerateCertificate = generateCertificate,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section 1", Content = "<p>Content 1</p>", RequiresAcknowledgment = true }
            }
        };

        var response = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", body);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<TalkIdDto>();
        return dto!.Id;
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

    /// <summary>
    /// Directly inserts a ScheduledTalk for the operator, bypassing POST /api/toolbox-talks/schedules
    /// + /process. That endpoint has a pre-existing, unrelated bug (CreateToolboxTalkScheduleCommandHandler.cs:136
    /// writes a DateTime with Kind=Unspecified computed from `DateTime.Today` to a `timestamptz` column,
    /// which Npgsql rejects) that 500s schedule creation outright — confirmed independent of this chunk by
    /// reproducing it against the pre-existing EmployeeCompletionTests helper, which shares the exact same
    /// `ScheduledDate = DateTime.Today.ToString(...)` pattern. Not fixed here per Scope Discipline.
    /// </summary>
    private async Task<Guid> ScheduleTalkToOperatorAsync(Guid talkId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var scheduledTalk = new ScheduledTalk
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantConstants.TenantId,
            ToolboxTalkId = talkId,
            EmployeeId = TestTenantConstants.Employees.OperatorEmployee,
            RequiredDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(7),
            Status = ScheduledTalkStatus.Pending,
            LanguageCode = "en",
        };

        db.Set<ScheduledTalk>().Add(scheduledTalk);
        await db.SaveChangesAsync();

        return scheduledTalk.Id;
    }

    /// <summary>Marks all sections of the given scheduled talk as read via the operator client.</summary>
    private async Task ReadAllSectionsAsync(Guid scheduledTalkId)
    {
        var detailResponse = await OperatorClient.GetAsync($"/api/my/toolbox-talks/{scheduledTalkId}");
        detailResponse.EnsureSuccessStatusCode();
        var talk = await detailResponse.Content.ReadFromJsonAsync<MyToolboxTalkDto>();

        foreach (var section in talk!.Sections.OrderBy(s => s.SectionNumber))
        {
            var readResponse = await OperatorClient.PostAsJsonAsync(
                $"/api/my/toolbox-talks/{scheduledTalkId}/sections/{section.SectionId}/read",
                new { TimeSpentSeconds = 5 });
            if (!readResponse.IsSuccessStatusCode)
            {
                var body = await readResponse.Content.ReadAsStringAsync();
                throw new Exception($"Mark section read failed: {readResponse.StatusCode} {body}");
            }
        }
    }

    private const string SampleSignature =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    private async Task<HttpResponseMessage> CompleteAsync(Guid scheduledTalkId) =>
        await OperatorClient.PostAsJsonAsync(
            $"/api/my/toolbox-talks/{scheduledTalkId}/complete",
            new { SignatureData = SampleSignature, SignedByName = "Test Operator" });

    private async Task<ToolboxTalkCertificate?> GetCertificateForScheduledTalkAsync(Guid scheduledTalkId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<ToolboxTalkCertificate>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.ScheduledTalkId == scheduledTalkId && !c.IsDeleted);
    }

    private static object CreateCourseBody(string title, bool generateCertificate, params Guid[] talkIds) => new
    {
        Title = title,
        Description = (string?)null,
        IsActive = true,
        RequireSequentialCompletion = false,
        RequiresRefresher = false,
        RefresherIntervalMonths = 12,
        GenerateCertificate = generateCertificate,
        AutoAssignToNewEmployees = false,
        AutoAssignDueDays = 14,
        Items = talkIds.Select((id, idx) => new { ToolboxTalkId = id, OrderIndex = idx, IsRequired = true }).ToArray()
    };

    #endregion

    #region Talk completion — email wiring

    [Fact]
    public async Task CompleteTalk_WithGenerateCertificate_CreatesCertificateAndSendsCompletionEmail()
    {
        // Arrange
        var talkId = await CreateTalkAsync();
        var scheduledTalkId = await ScheduleTalkToOperatorAsync(talkId);
        await ReadAllSectionsAsync(scheduledTalkId);

        // Act
        var response = await CompleteAsync(scheduledTalkId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var completion = await response.Content.ReadFromJsonAsync<ScheduledTalkCompletionResponseDto>();
        completion!.CertificateUrl.Should().NotBeNullOrEmpty();
        completion.CertificateGenerationFailed.Should().BeFalse();

        FakeToolboxTalkEmailService.CompletionEmails.Should().ContainSingle(
            e => e.ScheduledTalkId == scheduledTalkId && e.EmployeeId == TestTenantConstants.Employees.OperatorEmployee);
        FakeToolboxTalkEmailService.CompletionEmails.Single().CertificateUrl.Should().Be(completion.CertificateUrl);
    }

    [Fact]
    public async Task CompleteTalk_Repeated_ReturnsBadRequest_AndDoesNotSendSecondEmail()
    {
        // Arrange — complete once successfully
        var talkId = await CreateTalkAsync();
        var scheduledTalkId = await ScheduleTalkToOperatorAsync(talkId);
        await ReadAllSectionsAsync(scheduledTalkId);
        var firstResponse = await CompleteAsync(scheduledTalkId);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        FakeToolboxTalkEmailService.CompletionEmails.Should().HaveCount(1);

        // Act — attempt to complete the same scheduled talk again
        var secondResponse = await CompleteAsync(scheduledTalkId);

        // Assert — the completion guard rejects the repeat before certificate/email logic ever runs
        secondResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        FakeToolboxTalkEmailService.CompletionEmails.Should().HaveCount(1, "a repeat completion attempt must not send a second email");
    }

    [Fact]
    public async Task CompleteTalk_WhenEmailSendThrows_CompletesSuccessfullyAndFlagsCertificateEmailFailed()
    {
        // Arrange
        FakeToolboxTalkEmailService.ShouldThrowOnCompletionEmail = true;
        var talkId = await CreateTalkAsync();
        var scheduledTalkId = await ScheduleTalkToOperatorAsync(talkId);
        await ReadAllSectionsAsync(scheduledTalkId);

        // Act
        var response = await CompleteAsync(scheduledTalkId);

        // Assert — completion still succeeds and the certificate is still generated
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var completion = await response.Content.ReadFromJsonAsync<ScheduledTalkCompletionResponseDto>();
        completion!.CertificateUrl.Should().NotBeNullOrEmpty();
        completion.CertificateGenerationFailed.Should().BeFalse();

        var certificate = await GetCertificateForScheduledTalkAsync(scheduledTalkId);
        certificate.Should().NotBeNull();
        certificate!.CertificateEmailFailed.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteTalk_WhenCertificateGenerationThrows_CompletesSuccessfullyAndFlagsCertificateGenerationFailed_AndDoesNotSendEmail()
    {
        // Arrange
        FakeR2StorageService.ShouldThrowOnUploadCertificate = true;
        var talkId = await CreateTalkAsync();
        var scheduledTalkId = await ScheduleTalkToOperatorAsync(talkId);
        await ReadAllSectionsAsync(scheduledTalkId);

        // Act
        var response = await CompleteAsync(scheduledTalkId);

        // Assert — completion still succeeds, no certificate, generation failure now flagged (was previously
        // indistinguishable from "never attempted" — the swallow gap this chunk closes)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var completion = await response.Content.ReadFromJsonAsync<ScheduledTalkCompletionResponseDto>();
        completion!.CertificateUrl.Should().BeNullOrEmpty();
        completion.CertificateGenerationFailed.Should().BeTrue();

        FakeToolboxTalkEmailService.CompletionEmails.Should().BeEmpty("no certificate was created, so no completion email should fire");
    }

    #endregion

    #region Admin regenerate — email wiring

    [Fact]
    public async Task RegenerateCertificate_AfterPriorFailure_SendsCompletionEmail()
    {
        // Arrange — force generation failure on first completion attempt
        FakeR2StorageService.ShouldThrowOnUploadCertificate = true;
        var talkId = await CreateTalkAsync();
        var scheduledTalkId = await ScheduleTalkToOperatorAsync(talkId);
        await ReadAllSectionsAsync(scheduledTalkId);
        var completeResponse = await CompleteAsync(scheduledTalkId);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var completion = await completeResponse.Content.ReadFromJsonAsync<ScheduledTalkCompletionResponseDto>();
        completion!.CertificateGenerationFailed.Should().BeTrue();
        FakeToolboxTalkEmailService.CompletionEmails.Should().BeEmpty();

        // Act — admin regenerates once R2 recovers
        FakeR2StorageService.ShouldThrowOnUploadCertificate = false;
        var regenerateResponse = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{talkId}/completions/{completion.Id}/regenerate-certificate", null);

        // Assert
        regenerateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var regenerateResult = await regenerateResponse.Content.ReadFromJsonAsync<RegenerateResultDto>();
        regenerateResult!.CertificateUrl.Should().NotBeNullOrEmpty();

        FakeToolboxTalkEmailService.CompletionEmails.Should().ContainSingle(
            e => e.ScheduledTalkId == scheduledTalkId);
    }

    #endregion

    #region Course completion — email wiring

    [Fact]
    public async Task CourseCompletion_WithGenerateCertificate_CreatesCertificateAndSendsCourseCompletionEmail()
    {
        // Arrange — one-item published course so completing its only talk completes the course
        var talkId = await CreateTalkAsync(generateCertificate: false); // talk-level cert is skipped for course-scoped talks anyway
        await SetTalkStatusAsync(talkId, ToolboxTalkStatus.Published);

        var courseBody = CreateCourseBody(UniqueTitle("Cert Email Course"), generateCertificate: true, talkId);
        var courseResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/courses", courseBody);
        courseResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var course = await courseResponse.Content.ReadFromJsonAsync<CourseIdDto>();

        var assignBody = new
        {
            CourseId = course!.Id,
            Assignments = new[]
            {
                new { EmployeeId = TestTenantConstants.Employees.OperatorEmployee, IncludedTalkIds = (Guid[]?)null }
            }
        };
        var assignResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/course-assignments", assignBody);
        assignResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var assignResult = await assignResponse.Content.ReadFromJsonAsync<ResultWrapper<List<CourseAssignmentDto>>>();
        var assignment = assignResult!.Data!.Single();
        var scheduledTalkId = assignment.ScheduledTalks.Single().ScheduledTalkId;

        await ReadAllSectionsAsync(scheduledTalkId);

        // Act
        var completeResponse = await CompleteAsync(scheduledTalkId);

        // Assert
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        FakeToolboxTalkEmailService.CourseCompletionEmails.Should().ContainSingle(
            e => e.CourseAssignmentId == assignment.Id && e.EmployeeId == TestTenantConstants.Employees.OperatorEmployee);
        FakeToolboxTalkEmailService.CourseCompletionEmails.Single().CertificateUrl.Should().NotBeNullOrEmpty();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var courseAssignment = await db.Set<ToolboxTalkCourseAssignment>().IgnoreQueryFilters()
            .FirstAsync(a => a.Id == assignment.Id);
        courseAssignment.Status.Should().Be(CourseAssignmentStatus.Completed);
        courseAssignment.CertificateGenerationFailed.Should().BeFalse();
    }

    [Fact]
    public async Task CourseCompletion_WhenCertificateGenerationThrows_FlagsCertificateGenerationFailed_AndDoesNotSendEmail()
    {
        // Arrange
        var talkId = await CreateTalkAsync(generateCertificate: false);
        await SetTalkStatusAsync(talkId, ToolboxTalkStatus.Published);

        var courseBody = CreateCourseBody(UniqueTitle("Cert Email Course Fail"), generateCertificate: true, talkId);
        var courseResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/courses", courseBody);
        courseResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var course = await courseResponse.Content.ReadFromJsonAsync<CourseIdDto>();

        var assignBody = new
        {
            CourseId = course!.Id,
            Assignments = new[]
            {
                new { EmployeeId = TestTenantConstants.Employees.OperatorEmployee, IncludedTalkIds = (Guid[]?)null }
            }
        };
        var assignResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks/course-assignments", assignBody);
        assignResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var assignResult = await assignResponse.Content.ReadFromJsonAsync<ResultWrapper<List<CourseAssignmentDto>>>();
        var assignment = assignResult!.Data!.Single();
        var scheduledTalkId = assignment.ScheduledTalks.Single().ScheduledTalkId;

        await ReadAllSectionsAsync(scheduledTalkId);

        // Act
        FakeR2StorageService.ShouldThrowOnUploadCertificate = true;
        var completeResponse = await CompleteAsync(scheduledTalkId);

        // Assert — talk completion itself still succeeds (course cert failure must not block it)
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        FakeToolboxTalkEmailService.CourseCompletionEmails.Should().BeEmpty();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var courseAssignment = await db.Set<ToolboxTalkCourseAssignment>().IgnoreQueryFilters()
            .FirstAsync(a => a.Id == assignment.Id);
        courseAssignment.CertificateGenerationFailed.Should().BeTrue();
    }

    #endregion

    #region DTOs

    private record TalkIdDto(Guid Id);
    private record CourseIdDto(Guid Id);

    private record RegenerateResultDto(string CertificateUrl);

    private record MyToolboxTalkDto(List<MyToolboxTalkSectionDto> Sections);

    private record MyToolboxTalkSectionDto(Guid SectionId, int SectionNumber);

    private record ScheduledTalkCompletionResponseDto(
        Guid Id,
        DateTime CompletedAt,
        string? CertificateUrl,
        bool CertificateGenerationFailed);

    private record ResultWrapper<T>(bool Success, T? Data, string? Message, List<string>? Errors);

    private record CourseAssignmentDto(
        Guid Id,
        Guid CourseId,
        Guid EmployeeId,
        List<CourseScheduledTalkDto> ScheduledTalks);

    private record CourseScheduledTalkDto(Guid ScheduledTalkId, Guid ToolboxTalkId);

    #endregion
}
