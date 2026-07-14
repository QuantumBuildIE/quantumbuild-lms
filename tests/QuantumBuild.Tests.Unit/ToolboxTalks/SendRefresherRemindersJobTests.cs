using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

namespace QuantumBuild.Tests.Unit.ToolboxTalks;

/// <summary>
/// Unit tests for SendRefresherRemindersJob, focused on the Employee soft-delete
/// scoping fix (reminders must not be sent for offboarded employees).
/// </summary>
public class SendRefresherRemindersJobTests
{
    private readonly Mock<IToolboxTalksDbContext> _dbContextMock = new();
    private readonly Mock<ITenantRepository> _tenantRepositoryMock = new();
    private readonly Mock<IToolboxTalkEmailService> _emailServiceMock = new();
    private readonly Mock<ILogger<SendRefresherRemindersJob>> _loggerMock = new();

    private readonly Guid _tenantId = Guid.NewGuid();

    private SendRefresherRemindersJob CreateJob() => new(
        _dbContextMock.Object,
        _tenantRepositoryMock.Object,
        _emailServiceMock.Object,
        _loggerMock.Object);

    private void SetupTenant()
    {
        var tenant = new Tenant { Id = _tenantId, Name = "Test Tenant", IsActive = true };
        _tenantRepositoryMock
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Tenant> { tenant });
    }

    private ScheduledTalk BuildTwoWeekRefresherTalk(Employee employee)
    {
        var talk = ToolboxTalkBuilder.CreateBasicTalk();
        talk.TenantId = _tenantId;

        var scheduledTalk = ScheduledTalkBuilder.CreatePending(talk.Id, employee.Id);
        scheduledTalk.TenantId = _tenantId;
        scheduledTalk.IsRefresher = true;
        scheduledTalk.RefresherDueDate = DateTime.UtcNow.AddDays(10);
        scheduledTalk.ReminderSent2Weeks = false;
        scheduledTalk.ToolboxTalk = talk;
        scheduledTalk.Employee = employee;

        return scheduledTalk;
    }

    private void SetupDbSets(ScheduledTalk scheduledTalk)
    {
        _dbContextMock.Setup(c => c.ScheduledTalks).Returns(MockDbSetHelper.Create(scheduledTalk));
        _dbContextMock.Setup(c => c.ToolboxTalkCourseAssignments).Returns(MockDbSetHelper.Create<ToolboxTalkCourseAssignment>());
        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [Fact]
    public async Task SendRefresherReminders_SoftDeletedEmployee_NotSentReminder()
    {
        // Arrange
        SetupTenant();

        var employee = EmployeeBuilder.CreateActive("Deleted", "Employee");
        employee.TenantId = _tenantId;
        employee.IsDeleted = true;

        var scheduledTalk = BuildTwoWeekRefresherTalk(employee);
        SetupDbSets(scheduledTalk);

        var sut = CreateJob();

        // Act
        await sut.ExecuteAsync(CancellationToken.None);

        // Assert
        _emailServiceMock.Verify(
            e => e.SendRefresherReminderAsync(It.IsAny<ScheduledTalk>(), It.IsAny<Employee>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        scheduledTalk.ReminderSent2Weeks.Should().BeFalse();
    }

    [Fact]
    public async Task SendRefresherReminders_ActiveEmployee_SentReminder()
    {
        // Arrange
        SetupTenant();

        var employee = EmployeeBuilder.CreateActive("Active", "Employee");
        employee.TenantId = _tenantId;
        employee.IsDeleted = false;

        var scheduledTalk = BuildTwoWeekRefresherTalk(employee);
        SetupDbSets(scheduledTalk);

        _emailServiceMock
            .Setup(e => e.SendRefresherReminderAsync(It.IsAny<ScheduledTalk>(), It.IsAny<Employee>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateJob();

        // Act
        await sut.ExecuteAsync(CancellationToken.None);

        // Assert
        _emailServiceMock.Verify(
            e => e.SendRefresherReminderAsync(
                It.Is<ScheduledTalk>(st => st.Id == scheduledTalk.Id),
                It.Is<Employee>(emp => emp.Id == employee.Id),
                "2 weeks",
                It.IsAny<CancellationToken>()),
            Times.Once);

        scheduledTalk.ReminderSent2Weeks.Should().BeTrue();
    }
}
