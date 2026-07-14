using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using Moq;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Tests.Common.Builders;
using System.Linq.Expressions;

namespace QuantumBuild.Tests.Unit.ToolboxTalks;

/// <summary>
/// Unit tests for RefresherSchedulingService
/// </summary>
public class RefresherSchedulingServiceTests
{
    private readonly Mock<IToolboxTalksDbContext> _dbContextMock;
    private readonly Mock<ICoreDbContext> _coreDbContextMock;
    private readonly Mock<IToolboxTalkEmailService> _emailServiceMock;
    private readonly Mock<ILogger<RefresherSchedulingService>> _loggerMock;

    private readonly List<ToolboxTalk> _toolboxTalks = new();
    private readonly List<ScheduledTalk> _scheduledTalks = new();
    private readonly List<ToolboxTalkCourse> _courses = new();
    private readonly List<ToolboxTalkCourseAssignment> _courseAssignments = new();
    private readonly List<Employee> _employees = new();

    private readonly Guid _tenantId = Guid.NewGuid();

    public RefresherSchedulingServiceTests()
    {
        _dbContextMock = new Mock<IToolboxTalksDbContext>();
        _coreDbContextMock = new Mock<ICoreDbContext>();
        _emailServiceMock = new Mock<IToolboxTalkEmailService>();
        _loggerMock = new Mock<ILogger<RefresherSchedulingService>>();

        SetupDbContextMocks();

        _emailServiceMock
            .Setup(e => e.SendTalkAssignmentEmailAsync(It.IsAny<ScheduledTalk>(), It.IsAny<Employee>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _emailServiceMock
            .Setup(e => e.SendCourseAssignmentEmailAsync(It.IsAny<ToolboxTalkCourse>(), It.IsAny<Employee>(), It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private RefresherSchedulingService CreateService() => new(
        _dbContextMock.Object,
        _coreDbContextMock.Object,
        _emailServiceMock.Object,
        _loggerMock.Object);

    #region Standalone talk refresher

    [Fact]
    public async Task ScheduleRefresher_Standalone_SendsAssignmentEmailAfterSave()
    {
        // Arrange
        var talk = AddToolboxTalk(requiresRefresher: true);
        var employee = AddEmployee();
        var completedTalk = ScheduledTalkBuilder.CreateCompleted(talk.Id, employee.Id);
        completedTalk.TenantId = _tenantId;

        var sut = CreateService();

        // Act
        await sut.ScheduleRefresherIfRequired(completedTalk);

        // Assert
        _scheduledTalks.Should().ContainSingle(st => st.IsRefresher);
        var refresher = _scheduledTalks.Single(st => st.IsRefresher);

        _emailServiceMock.Verify(
            e => e.SendTalkAssignmentEmailAsync(
                It.Is<ScheduledTalk>(st => st.Id == refresher.Id && st.ToolboxTalk != null),
                It.Is<Employee>(e => e.Id == employee.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ScheduleRefresher_Standalone_EmployeeNotFound_LogsErrorAndSkipsEmail()
    {
        // Arrange
        var talk = AddToolboxTalk(requiresRefresher: true);
        var missingEmployeeId = Guid.NewGuid();
        var completedTalk = ScheduledTalkBuilder.CreateCompleted(talk.Id, missingEmployeeId);
        completedTalk.TenantId = _tenantId;

        var sut = CreateService();

        // Act
        await sut.ScheduleRefresherIfRequired(completedTalk);

        // Assert
        _scheduledTalks.Should().ContainSingle(st => st.IsRefresher);

        _emailServiceMock.Verify(
            e => e.SendTalkAssignmentEmailAsync(It.IsAny<ScheduledTalk>(), It.IsAny<Employee>(), It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyLogError(Times.Once());
    }

    [Fact]
    public async Task ScheduleRefresher_Standalone_EmailThrows_LogsErrorAndSwallows()
    {
        // Arrange
        var talk = AddToolboxTalk(requiresRefresher: true);
        var employee = AddEmployee();
        var completedTalk = ScheduledTalkBuilder.CreateCompleted(talk.Id, employee.Id);
        completedTalk.TenantId = _tenantId;

        _emailServiceMock
            .Setup(e => e.SendTalkAssignmentEmailAsync(It.IsAny<ScheduledTalk>(), It.IsAny<Employee>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP unavailable"));

        var sut = CreateService();

        // Act
        var exception = await Record.ExceptionAsync(() => sut.ScheduleRefresherIfRequired(completedTalk));

        // Assert
        exception.Should().BeNull();
        _scheduledTalks.Should().ContainSingle(st => st.IsRefresher);

        VerifyLogError(Times.Once());
    }

    #endregion

    #region Course refresher

    [Fact]
    public async Task ScheduleRefresher_Course_SendsOneEmailAfterSave()
    {
        // Arrange
        var course = AddCourse(requiresRefresher: true, itemCount: 3);
        var employee = AddEmployee();
        var completedAssignment = new ToolboxTalkCourseAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CourseId = course.Id,
            EmployeeId = employee.Id,
            AssignedAt = DateTime.UtcNow.AddMonths(-12),
            CompletedAt = DateTime.UtcNow,
            Status = CourseAssignmentStatus.Completed,
        };

        var sut = CreateService();

        // Act
        await sut.ScheduleRefresherIfRequired(completedAssignment);

        // Assert
        _courseAssignments.Should().ContainSingle(a => a.IsRefresher);
        var refresherAssignment = _courseAssignments.Single(a => a.IsRefresher);

        _scheduledTalks.Should().HaveCount(3);
        _scheduledTalks.Should().OnlyContain(st => st.CourseAssignmentId == refresherAssignment.Id && st.IsRefresher);

        _emailServiceMock.Verify(
            e => e.SendCourseAssignmentEmailAsync(
                It.Is<ToolboxTalkCourse>(c => c.Id == course.Id),
                It.Is<Employee>(e => e.Id == employee.Id),
                3,
                refresherAssignment.DueDate,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ScheduleRefresher_Course_EmployeeNotFound_LogsErrorAndSkipsEmail()
    {
        // Arrange
        var course = AddCourse(requiresRefresher: true, itemCount: 2);
        var missingEmployeeId = Guid.NewGuid();
        var completedAssignment = new ToolboxTalkCourseAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CourseId = course.Id,
            EmployeeId = missingEmployeeId,
            AssignedAt = DateTime.UtcNow.AddMonths(-12),
            CompletedAt = DateTime.UtcNow,
            Status = CourseAssignmentStatus.Completed,
        };

        var sut = CreateService();

        // Act
        await sut.ScheduleRefresherIfRequired(completedAssignment);

        // Assert
        _courseAssignments.Should().ContainSingle(a => a.IsRefresher);

        _emailServiceMock.Verify(
            e => e.SendCourseAssignmentEmailAsync(It.IsAny<ToolboxTalkCourse>(), It.IsAny<Employee>(), It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()),
            Times.Never);

        VerifyLogError(Times.Once());
    }

    [Fact]
    public async Task ScheduleRefresher_Course_EmailThrows_LogsErrorAndSwallows()
    {
        // Arrange
        var course = AddCourse(requiresRefresher: true, itemCount: 2);
        var employee = AddEmployee();
        var completedAssignment = new ToolboxTalkCourseAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CourseId = course.Id,
            EmployeeId = employee.Id,
            AssignedAt = DateTime.UtcNow.AddMonths(-12),
            CompletedAt = DateTime.UtcNow,
            Status = CourseAssignmentStatus.Completed,
        };

        _emailServiceMock
            .Setup(e => e.SendCourseAssignmentEmailAsync(It.IsAny<ToolboxTalkCourse>(), It.IsAny<Employee>(), It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP unavailable"));

        var sut = CreateService();

        // Act
        var exception = await Record.ExceptionAsync(() => sut.ScheduleRefresherIfRequired(completedAssignment));

        // Assert
        exception.Should().BeNull();
        _courseAssignments.Should().ContainSingle(a => a.IsRefresher);

        VerifyLogError(Times.Once());
    }

    #endregion

    #region Helper methods

    private void VerifyLogError(Times times)
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }

    private void SetupDbContextMocks()
    {
        var toolboxTalksMock = CreateMockDbSet(_toolboxTalks);
        var scheduledTalksMock = CreateMockDbSet(_scheduledTalks);
        var coursesMock = CreateMockDbSet(_courses);
        var courseAssignmentsMock = CreateMockDbSet(_courseAssignments);
        var employeesMock = CreateMockDbSet(_employees);

        _dbContextMock.Setup(x => x.ToolboxTalks).Returns(toolboxTalksMock.Object);
        _dbContextMock.Setup(x => x.ScheduledTalks).Returns(scheduledTalksMock.Object);
        _dbContextMock.Setup(x => x.ToolboxTalkCourses).Returns(coursesMock.Object);
        _dbContextMock.Setup(x => x.ToolboxTalkCourseAssignments).Returns(courseAssignmentsMock.Object);
        _dbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _coreDbContextMock.Setup(x => x.Employees).Returns(employeesMock.Object);
    }

    private Mock<DbSet<T>> CreateMockDbSet<T>(List<T> data) where T : class
    {
        var queryable = data.AsQueryable();
        var mockSet = new Mock<DbSet<T>>();

        mockSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(new TestAsyncQueryProvider<T>(queryable.Provider));
        mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
        mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
        mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(() => queryable.GetEnumerator());
        mockSet.As<IAsyncEnumerable<T>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<T>(queryable.GetEnumerator()));

        mockSet.Setup(x => x.Add(It.IsAny<T>())).Callback<T>(item =>
        {
            var idProperty = typeof(T).GetProperty("Id");
            if (idProperty != null && idProperty.PropertyType == typeof(Guid))
            {
                var currentId = (Guid)idProperty.GetValue(item)!;
                if (currentId == Guid.Empty)
                {
                    idProperty.SetValue(item, Guid.NewGuid());
                }
            }
            data.Add(item);
        });

        return mockSet;
    }

    private ToolboxTalk AddToolboxTalk(bool requiresRefresher, int refresherIntervalMonths = 12)
    {
        var talk = ToolboxTalkBuilder.CreateBasicTalk();
        talk.TenantId = _tenantId;
        talk.RequiresRefresher = requiresRefresher;
        talk.RefresherIntervalMonths = refresherIntervalMonths;
        _toolboxTalks.Add(talk);
        return talk;
    }

    private Employee AddEmployee()
    {
        var employee = EmployeeBuilder.CreateActive("Test", "Operator");
        employee.TenantId = _tenantId;
        _employees.Add(employee);
        return employee;
    }

    private ToolboxTalkCourse AddCourse(bool requiresRefresher, int itemCount, int refresherIntervalMonths = 12)
    {
        var course = new ToolboxTalkCourse
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Title = "Test Course",
            RequiresRefresher = requiresRefresher,
            RefresherIntervalMonths = refresherIntervalMonths,
        };

        for (var i = 0; i < itemCount; i++)
        {
            var itemTalk = AddToolboxTalk(requiresRefresher: false);
            course.CourseItems.Add(new ToolboxTalkCourseItem
            {
                Id = Guid.NewGuid(),
                CourseId = course.Id,
                ToolboxTalkId = itemTalk.Id,
                OrderIndex = i,
                IsRequired = true,
            });
        }

        _courses.Add(course);
        return course;
    }

    #endregion

    #region Test Async Helpers

    private class TestAsyncQueryProvider<T> : IAsyncQueryProvider
    {
        private readonly IQueryProvider _inner;

        public TestAsyncQueryProvider(IQueryProvider inner) => _inner = inner;

        public IQueryable CreateQuery(Expression expression) =>
            new TestAsyncEnumerable<T>(expression);

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
            new TestAsyncEnumerable<TElement>(expression);

        public object? Execute(Expression expression) =>
            _inner.Execute(expression);

        public TResult Execute<TResult>(Expression expression) =>
            _inner.Execute<TResult>(expression);

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var resultType = typeof(TResult).GetGenericArguments()[0];
            var executionResult = typeof(IQueryProvider)
                .GetMethod(
                    name: nameof(IQueryProvider.Execute),
                    genericParameterCount: 1,
                    types: new[] { typeof(Expression) })!
                .MakeGenericMethod(resultType)
                .Invoke(this, new[] { expression });

            return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType)
                .Invoke(null, new[] { executionResult })!;
        }
    }

    private class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(Expression expression) : base(expression) { }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());

        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
    }

    private class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> _inner;

        public TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;

        public T Current => _inner.Current;

        public ValueTask<bool> MoveNextAsync() => new(_inner.MoveNext());

        public ValueTask DisposeAsync()
        {
            _inner.Dispose();
            return default;
        }
    }

    #endregion
}
