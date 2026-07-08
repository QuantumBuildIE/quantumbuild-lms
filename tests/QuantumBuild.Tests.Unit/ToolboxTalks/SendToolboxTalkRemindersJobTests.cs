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
/// Unit tests for SendToolboxTalkRemindersJob, focused on the Employee soft-delete
/// scoping fix (reminders must not be sent for offboarded employees).
/// </summary>
public class SendToolboxTalkRemindersJobTests
{
    private readonly Mock<IToolboxTalksDbContext> _dbContextMock = new();
    private readonly Mock<ICoreDbContext> _coreDbContextMock = new();
    private readonly Mock<ITenantRepository> _tenantRepositoryMock = new();
    private readonly Mock<IToolboxTalkEmailService> _emailServiceMock = new();
    private readonly Mock<ILogger<SendToolboxTalkRemindersJob>> _loggerMock = new();

    private readonly Guid _tenantId = Guid.NewGuid();

    private SendToolboxTalkRemindersJob CreateJob() => new(
        _dbContextMock.Object,
        _coreDbContextMock.Object,
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

    private ScheduledTalk BuildOverdueTalk(Employee employee)
    {
        var talk = ToolboxTalkBuilder.CreateBasicTalk();
        talk.TenantId = _tenantId;

        var scheduledTalk = ScheduledTalkBuilder.CreatePending(talk.Id, employee.Id);
        scheduledTalk.TenantId = _tenantId;
        scheduledTalk.DueDate = DateTime.UtcNow.AddDays(-3);
        scheduledTalk.RemindersSent = 0;
        scheduledTalk.ToolboxTalk = talk;
        scheduledTalk.Employee = employee;

        return scheduledTalk;
    }

    private void SetupDbSets(ScheduledTalk scheduledTalk)
    {
        _dbContextMock.Setup(c => c.ScheduledTalks).Returns(MockDbSetHelper.Create(scheduledTalk));
        _dbContextMock.Setup(c => c.ToolboxTalkSettings).Returns(MockDbSetHelper.Create<ToolboxTalkSettings>());
        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [Fact]
    public async Task SendToolboxTalkReminders_SoftDeletedEmployee_NotSentReminder()
    {
        // Arrange
        SetupTenant();

        var employee = EmployeeBuilder.CreateActive("Deleted", "Employee");
        employee.TenantId = _tenantId;
        employee.IsDeleted = true;

        var scheduledTalk = BuildOverdueTalk(employee);
        SetupDbSets(scheduledTalk);

        var sut = CreateJob();

        // Act
        await sut.ExecuteAsync(CancellationToken.None);

        // Assert
        _emailServiceMock.Verify(
            e => e.SendReminderEmailAsync(It.IsAny<ScheduledTalk>(), It.IsAny<Employee>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

        scheduledTalk.RemindersSent.Should().Be(0);
    }

    [Fact]
    public async Task SendToolboxTalkReminders_ActiveEmployee_SentReminder()
    {
        // Arrange
        SetupTenant();

        var employee = EmployeeBuilder.CreateActive("Active", "Employee");
        employee.TenantId = _tenantId;
        employee.IsDeleted = false;

        var scheduledTalk = BuildOverdueTalk(employee);
        SetupDbSets(scheduledTalk);

        _emailServiceMock
            .Setup(e => e.SendReminderEmailAsync(It.IsAny<ScheduledTalk>(), It.IsAny<Employee>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateJob();

        // Act
        await sut.ExecuteAsync(CancellationToken.None);

        // Assert
        _emailServiceMock.Verify(
            e => e.SendReminderEmailAsync(
                It.Is<ScheduledTalk>(st => st.Id == scheduledTalk.Id),
                It.Is<Employee>(emp => emp.Id == employee.Id),
                1,
                It.IsAny<CancellationToken>()),
            Times.Once);

        scheduledTalk.RemindersSent.Should().Be(1);
    }
}
