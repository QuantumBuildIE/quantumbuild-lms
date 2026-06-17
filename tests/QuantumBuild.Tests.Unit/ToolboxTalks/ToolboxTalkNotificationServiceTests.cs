using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Abstractions.Email;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services;

namespace QuantumBuild.Tests.Unit.ToolboxTalks;

public class ToolboxTalkNotificationServiceTests
{
    private readonly Mock<IToolboxTalksDbContext> _context = new();
    private readonly Mock<UserManager<User>> _userManager;
    private readonly Mock<IEmailProvider> _emailProvider = new();
    private readonly Mock<IConfiguration> _configuration = new();
    private readonly Mock<ILogger<ToolboxTalkNotificationService>> _logger = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _talkId = Guid.NewGuid();

    public ToolboxTalkNotificationServiceTests()
    {
        var userStore = new Mock<IUserStore<User>>();
        _userManager = new Mock<UserManager<User>>(
            userStore.Object, null, null, null, null, null, null, null, null);

        _configuration.Setup(c => c["AppSettings:BaseUrl"]).Returns("https://certifiediq.ai");

        _emailProvider.Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmailSendResult.Succeeded());
    }

    private ToolboxTalkNotificationService CreateService() => new(
        _context.Object,
        _userManager.Object,
        _emailProvider.Object,
        _configuration.Object,
        _logger.Object);

    private void SetupSettings(
        bool notifyOnTranslationComplete = true,
        bool notifyOnValidationComplete = true,
        bool notifyOnFailure = true,
        bool notifyOnExternalReviewResponse = true)
    {
        var settings = new ToolboxTalkSettings
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            NotifyOnTranslationComplete = notifyOnTranslationComplete,
            NotifyOnValidationComplete = notifyOnValidationComplete,
            NotifyOnFailure = notifyOnFailure,
            NotifyOnExternalReviewResponse = notifyOnExternalReviewResponse,
        };
        var dbSet = MockDbSetHelper.Create(settings);
        _context.Setup(c => c.ToolboxTalkSettings).Returns(dbSet);
    }

    private void SetupNoSettings()
    {
        var dbSet = MockDbSetHelper.Create<ToolboxTalkSettings>();
        _context.Setup(c => c.ToolboxTalkSettings).Returns(dbSet);
    }

    private void SetupAdmins(params User[] admins)
    {
        _userManager.Setup(m => m.GetUsersInRoleAsync("Admin"))
            .ReturnsAsync(admins.ToList());
    }

    private User MakeAdmin(string email = "admin@test.com") => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        UserName = email,
        TenantId = _tenantId,
        IsActive = true,
        FirstName = "Admin",
        LastName = "User",
    };

    // ── NotifyTranslationCompleteAsync ───────────────────────────────────────

    [Fact]
    public async Task NotifyTranslationCompleteAsync_WhenToggleOff_DoesNotSendEmail()
    {
        SetupSettings(notifyOnTranslationComplete: false);
        SetupAdmins(MakeAdmin());
        var svc = CreateService();

        await svc.NotifyTranslationCompleteAsync(
            _tenantId, _talkId, "Test Talk",
            [new("Spanish", "es", true, null)]);

        _emailProvider.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotifyTranslationCompleteAsync_WhenNoSettings_SendsEmail()
    {
        SetupNoSettings();
        SetupAdmins(MakeAdmin());
        var svc = CreateService();

        await svc.NotifyTranslationCompleteAsync(
            _tenantId, _talkId, "Test Talk",
            [new("Spanish", "es", true, null)]);

        _emailProvider.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyTranslationCompleteAsync_WhenToggleOnAndAdmin_SendsEmail()
    {
        SetupSettings(notifyOnTranslationComplete: true);
        SetupAdmins(MakeAdmin());
        var svc = CreateService();

        await svc.NotifyTranslationCompleteAsync(
            _tenantId, _talkId, "Test Talk",
            [new("Spanish", "es", true, null), new("French", "fr", false, "Timeout")]);

        _emailProvider.Verify(e => e.SendAsync(
            It.Is<EmailMessage>(m => m.Subject.Contains("Test Talk")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyTranslationCompleteAsync_WithMultipleAdmins_SendsToEach()
    {
        SetupSettings();
        SetupAdmins(MakeAdmin("a@t.com"), MakeAdmin("b@t.com"));
        var svc = CreateService();

        await svc.NotifyTranslationCompleteAsync(
            _tenantId, _talkId, "Test Talk",
            [new("Spanish", "es", true, null)]);

        _emailProvider.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task NotifyTranslationCompleteAsync_WhenNoAdmins_SendsNoEmail()
    {
        SetupSettings();
        SetupAdmins();
        var svc = CreateService();

        await svc.NotifyTranslationCompleteAsync(
            _tenantId, _talkId, "Test Talk",
            [new("Spanish", "es", true, null)]);

        _emailProvider.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotifyTranslationCompleteAsync_WhenEmptyResults_SendsNoEmail()
    {
        SetupSettings();
        SetupAdmins(MakeAdmin());
        var svc = CreateService();

        await svc.NotifyTranslationCompleteAsync(_tenantId, _talkId, "Test Talk", []);

        _emailProvider.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── NotifyValidationCompleteAsync ────────────────────────────────────────

    [Fact]
    public async Task NotifyValidationCompleteAsync_WhenToggleOff_DoesNotSendEmail()
    {
        SetupSettings(notifyOnValidationComplete: false);
        SetupAdmins(MakeAdmin());
        var svc = CreateService();

        await svc.NotifyValidationCompleteAsync(
            _tenantId, _talkId, "Test Talk", "Spanish", "Pass", 88.5, 5, 5);

        _emailProvider.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotifyValidationCompleteAsync_WhenToggleOn_SendsEmailWithOutcome()
    {
        SetupSettings();
        SetupAdmins(MakeAdmin());
        var svc = CreateService();

        await svc.NotifyValidationCompleteAsync(
            _tenantId, _talkId, "Test Talk", "Spanish", "Fail", 45.0, 2, 5);

        _emailProvider.Verify(e => e.SendAsync(
            It.Is<EmailMessage>(m => m.Subject.Contains("Fail")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── NotifyFailureAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task NotifyFailureAsync_WhenToggleOff_DoesNotSendEmail()
    {
        SetupSettings(notifyOnFailure: false);
        SetupAdmins(MakeAdmin());
        var svc = CreateService();

        await svc.NotifyFailureAsync(_tenantId, _talkId, "Test Talk", "Translation", "Timeout");

        _emailProvider.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotifyFailureAsync_WhenToggleOn_SendsEmailWithContext()
    {
        SetupSettings();
        SetupAdmins(MakeAdmin());
        var svc = CreateService();

        await svc.NotifyFailureAsync(_tenantId, _talkId, "Test Talk", "Translation validation", "Connection refused");

        _emailProvider.Verify(e => e.SendAsync(
            It.Is<EmailMessage>(m => m.Subject.Contains("Pipeline Failure")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── NotifyExternalReviewResponseAsync ───────────────────────────────────

    [Fact]
    public async Task NotifyExternalReviewResponseAsync_WhenToggleOff_DoesNotSendEmail()
    {
        SetupSettings(notifyOnExternalReviewResponse: false);
        SetupAdmins(MakeAdmin());
        var svc = CreateService();

        await svc.NotifyExternalReviewResponseAsync(_tenantId, _talkId, "Test Talk", "Spanish", accepted: true);

        _emailProvider.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotifyExternalReviewResponseAsync_WhenAccepted_SubjectSaysAccepted()
    {
        SetupSettings();
        SetupAdmins(MakeAdmin());
        var svc = CreateService();

        await svc.NotifyExternalReviewResponseAsync(_tenantId, _talkId, "Test Talk", "Spanish", accepted: true);

        _emailProvider.Verify(e => e.SendAsync(
            It.Is<EmailMessage>(m => m.Subject.Contains("Accepted")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyExternalReviewResponseAsync_WhenRejected_SubjectSaysRejected()
    {
        SetupSettings();
        SetupAdmins(MakeAdmin());
        var svc = CreateService();

        await svc.NotifyExternalReviewResponseAsync(_tenantId, _talkId, "Test Talk", "Spanish", accepted: false);

        _emailProvider.Verify(e => e.SendAsync(
            It.Is<EmailMessage>(m => m.Subject.Contains("Rejected")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Resilience ───────────────────────────────────────────────────────────

    [Fact]
    public async Task NotifyTranslationCompleteAsync_WhenEmailProviderThrows_DoesNotPropagate()
    {
        SetupSettings();
        SetupAdmins(MakeAdmin());
        _emailProvider.Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Provider down"));

        var svc = CreateService();

        // Should not throw
        await svc.NotifyTranslationCompleteAsync(
            _tenantId, _talkId, "Test Talk",
            [new("Spanish", "es", true, null)]);
    }

    [Fact]
    public async Task NotifyFailureAsync_WhenUserManagerThrows_DoesNotPropagate()
    {
        SetupSettings();
        _userManager.Setup(m => m.GetUsersInRoleAsync("Admin"))
            .ThrowsAsync(new InvalidOperationException("Identity error"));

        var svc = CreateService();

        // Should not throw
        await svc.NotifyFailureAsync(_tenantId, _talkId, "Test Talk", "Validation", "Error");
    }

    /// <summary>
    /// Admin from a different tenant should not receive notifications.
    /// </summary>
    [Fact]
    public async Task NotifyTranslationCompleteAsync_FiltersAdminsByTenant()
    {
        SetupSettings();
        var ownAdmin = MakeAdmin("own@test.com");
        var otherAdmin = new User
        {
            Id = Guid.NewGuid(),
            Email = "other@test.com",
            UserName = "other@test.com",
            TenantId = Guid.NewGuid(), // different tenant
            IsActive = true,
            FirstName = "Other",
            LastName = "Admin",
        };
        SetupAdmins(ownAdmin, otherAdmin);
        var svc = CreateService();

        await svc.NotifyTranslationCompleteAsync(
            _tenantId, _talkId, "Test Talk",
            [new("Spanish", "es", true, null)]);

        // Only one email — to ownAdmin
        _emailProvider.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

/// <summary>
/// Helper to create a synchronous IQueryable-backed mock DbSet usable with EF Core async extensions.
/// </summary>
internal static class MockDbSetHelper
{
    public static DbSet<T> Create<T>(params T[] data) where T : class
    {
        var list = data.ToList();
        var queryable = list.AsQueryable();
        var mock = new Mock<DbSet<T>>();

        mock.As<IAsyncEnumerable<T>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<T>(queryable.GetEnumerator()));

        mock.As<IQueryable<T>>().Setup(m => m.Provider).Returns(
            new TestAsyncQueryProvider<T>(queryable.Provider));
        mock.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
        mock.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
        mock.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(() => queryable.GetEnumerator());

        return mock.Object;
    }
}

internal class TestAsyncQueryProvider<TEntity>(IQueryProvider inner) : IAsyncQueryProvider
{
    public IQueryable CreateQuery(System.Linq.Expressions.Expression expression)
        => new TestAsyncEnumerable<TEntity>(expression);

    public IQueryable<TElement> CreateQuery<TElement>(System.Linq.Expressions.Expression expression)
        => new TestAsyncEnumerable<TElement>(expression);

    public object? Execute(System.Linq.Expressions.Expression expression)
        => inner.Execute(expression);

    public TResult Execute<TResult>(System.Linq.Expressions.Expression expression)
        => inner.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(System.Linq.Expressions.Expression expression, CancellationToken cancellationToken = default)
    {
        var resultType = typeof(TResult).GetGenericArguments()[0];
        // Use GetMethods to avoid AmbiguousMatchException — IQueryProvider has both
        // a generic and non-generic Execute overload with identical parameter lists.
        var executeMethod = typeof(IQueryProvider)
            .GetMethods()
            .Single(m => m.Name == nameof(IQueryProvider.Execute) && m.IsGenericMethod)
            .MakeGenericMethod(resultType);
        var result = executeMethod.Invoke(inner, [expression]);
        return (TResult)typeof(Task)
            .GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(resultType)
            .Invoke(null, [result])!;
    }
}

internal class TestAsyncEnumerable<T>(System.Linq.Expressions.Expression expression)
    : EnumerableQuery<T>(expression), IAsyncEnumerable<T>, IQueryable<T>
{
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());

    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
}

internal class TestAsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
{
    public T Current => inner.Current;

    public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(inner.MoveNext());

    public ValueTask DisposeAsync()
    {
        inner.Dispose();
        return ValueTask.CompletedTask;
    }
}
