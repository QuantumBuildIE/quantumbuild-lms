using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Common.Builders;

/// <summary>
/// Fluent builder for creating ScheduledTalk entities in tests.
/// </summary>
public class ScheduledTalkBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _tenantId = TestTenant.TestTenantConstants.TenantId;
    private Guid _toolboxTalkId;
    private Guid _employeeId;
    private Guid? _scheduleId = null;
    private DateTime _requiredDate = DateTime.UtcNow;
    private DateTime _dueDate = DateTime.UtcNow.AddDays(7);
    private ScheduledTalkStatus _status = ScheduledTalkStatus.Pending;
    private int _remindersSent = 0;
    private DateTime? _lastReminderAt = null;
    private string _languageCode = "en";
    private int _videoWatchPercent = 0;

    public ScheduledTalkBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public ScheduledTalkBuilder WithTenantId(Guid tenantId)
    {
        _tenantId = tenantId;
        return this;
    }

    public ScheduledTalkBuilder ForTalk(Guid toolboxTalkId)
    {
        _toolboxTalkId = toolboxTalkId;
        return this;
    }

    public ScheduledTalkBuilder ForEmployee(Guid employeeId)
    {
        _employeeId = employeeId;
        return this;
    }

    public ScheduledTalkBuilder FromSchedule(Guid scheduleId)
    {
        _scheduleId = scheduleId;
        return this;
    }

    public ScheduledTalkBuilder WithRequiredDate(DateTime requiredDate)
    {
        _requiredDate = requiredDate;
        return this;
    }

    public ScheduledTalkBuilder WithDueDate(DateTime dueDate)
    {
        _dueDate = dueDate;
        return this;
    }

    public ScheduledTalkBuilder WithStatus(ScheduledTalkStatus status)
    {
        _status = status;
        return this;
    }

    public ScheduledTalkBuilder AsPending()
    {
        _status = ScheduledTalkStatus.Pending;
        return this;
    }

    public ScheduledTalkBuilder AsInProgress(int videoWatchPercent = 50)
    {
        _status = ScheduledTalkStatus.InProgress;
        _videoWatchPercent = videoWatchPercent;
        return this;
    }

    public ScheduledTalkBuilder AsCompleted()
    {
        _status = ScheduledTalkStatus.Completed;
        _videoWatchPercent = 100;
        return this;
    }

    public ScheduledTalkBuilder AsOverdue(int remindersSent = 3)
    {
        _status = ScheduledTalkStatus.Overdue;
        _dueDate = DateTime.UtcNow.AddDays(-1);
        _remindersSent = remindersSent;
        _lastReminderAt = DateTime.UtcNow.AddDays(-1);
        return this;
    }

    public ScheduledTalkBuilder AsCancelled()
    {
        _status = ScheduledTalkStatus.Cancelled;
        return this;
    }

    public ScheduledTalkBuilder WithReminders(int count, DateTime? lastSentAt = null)
    {
        _remindersSent = count;
        _lastReminderAt = lastSentAt ?? DateTime.UtcNow.AddDays(-1);
        return this;
    }

    public ScheduledTalkBuilder WithLanguage(string languageCode)
    {
        _languageCode = languageCode;
        return this;
    }

    public ScheduledTalkBuilder WithVideoProgress(int percent)
    {
        _videoWatchPercent = percent;
        return this;
    }

    public ScheduledTalk Build()
    {
        return new ScheduledTalk
        {
            Id = _id,
            TenantId = _tenantId,
            ToolboxTalkId = _toolboxTalkId,
            EmployeeId = _employeeId,
            ScheduleId = _scheduleId,
            RequiredDate = _requiredDate,
            DueDate = _dueDate,
            Status = _status,
            RemindersSent = _remindersSent,
            LastReminderAt = _lastReminderAt,
            LanguageCode = _languageCode,
            VideoWatchPercent = _videoWatchPercent,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test-builder"
        };
    }

    /// <summary>
    /// Creates a pending scheduled talk.
    /// </summary>
    public static ScheduledTalk CreatePending(Guid toolboxTalkId, Guid employeeId, Guid? id = null)
    {
        return new ScheduledTalkBuilder()
            .WithId(id ?? Guid.NewGuid())
            .ForTalk(toolboxTalkId)
            .ForEmployee(employeeId)
            .AsPending()
            .Build();
    }

    /// <summary>
    /// Creates an in-progress scheduled talk.
    /// </summary>
    public static ScheduledTalk CreateInProgress(Guid toolboxTalkId, Guid employeeId, int videoWatchPercent = 50, Guid? id = null)
    {
        return new ScheduledTalkBuilder()
            .WithId(id ?? Guid.NewGuid())
            .ForTalk(toolboxTalkId)
            .ForEmployee(employeeId)
            .AsInProgress(videoWatchPercent)
            .Build();
    }

    /// <summary>
    /// Creates a completed scheduled talk.
    /// </summary>
    public static ScheduledTalk CreateCompleted(Guid toolboxTalkId, Guid employeeId, Guid? id = null)
    {
        return new ScheduledTalkBuilder()
            .WithId(id ?? Guid.NewGuid())
            .ForTalk(toolboxTalkId)
            .ForEmployee(employeeId)
            .AsCompleted()
            .Build();
    }

    /// <summary>
    /// Creates an overdue scheduled talk.
    /// </summary>
    public static ScheduledTalk CreateOverdue(Guid toolboxTalkId, Guid employeeId, int remindersSent = 3, Guid? id = null)
    {
        return new ScheduledTalkBuilder()
            .WithId(id ?? Guid.NewGuid())
            .ForTalk(toolboxTalkId)
            .ForEmployee(employeeId)
            .AsOverdue(remindersSent)
            .Build();
    }
}
