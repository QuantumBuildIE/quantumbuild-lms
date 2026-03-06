using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

namespace QuantumBuild.Tests.Unit.ToolboxTalks.Validation;

public class SafetyClassificationServiceTests
{
    private readonly Mock<IToolboxTalksDbContext> _dbContext = new();

    private SafetyClassificationService CreateService()
    {
        var logger = Mock.Of<ILogger<SafetyClassificationService>>();
        return new SafetyClassificationService(_dbContext.Object, logger);
    }

    private void SetupGlossaryTerms(List<SafetyGlossaryTerm> terms)
    {
        var queryable = terms.AsQueryable();
        var mockDbSet = new Mock<DbSet<SafetyGlossaryTerm>>();

        mockDbSet.As<IAsyncEnumerable<SafetyGlossaryTerm>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<SafetyGlossaryTerm>(queryable.GetEnumerator()));

        mockDbSet.As<IQueryable<SafetyGlossaryTerm>>()
            .Setup(m => m.Provider)
            .Returns(new TestAsyncQueryProvider<SafetyGlossaryTerm>(queryable.Provider));
        mockDbSet.As<IQueryable<SafetyGlossaryTerm>>()
            .Setup(m => m.Expression).Returns(queryable.Expression);
        mockDbSet.As<IQueryable<SafetyGlossaryTerm>>()
            .Setup(m => m.ElementType).Returns(queryable.ElementType);
        mockDbSet.As<IQueryable<SafetyGlossaryTerm>>()
            .Setup(m => m.GetEnumerator()).Returns(queryable.GetEnumerator());

        _dbContext.Setup(d => d.SafetyGlossaryTerms).Returns(mockDbSet.Object);
    }

    [Fact]
    public async Task ClassifyAsync_TextWithDoNot_IsSafetyCritical()
    {
        SetupGlossaryTerms([]);
        var sut = CreateService();

        var result = await sut.ClassifyAsync(
            "Do not operate machinery without training",
            "construction", "pl");

        result.IsSafetyCritical.Should().BeTrue();
        result.CriticalTermsFound.Should().Contain(t => t.Contains("Do not"));
    }

    [Fact]
    public async Task ClassifyAsync_TextWithPPE_MatchingGlossaryTerm_IsSafetyCritical()
    {
        var glossary = new SafetyGlossary
        {
            Id = Guid.NewGuid(),
            SectorKey = "construction",
            SectorName = "Construction",
            IsActive = true
        };
        var term = new SafetyGlossaryTerm
        {
            Id = Guid.NewGuid(),
            GlossaryId = glossary.Id,
            Glossary = glossary,
            EnglishTerm = "PPE",
            Category = "Equipment",
            IsCritical = true,
            Translations = """{"pl":"ŚOI","ro":"EIP"}"""
        };
        SetupGlossaryTerms([term]);
        var sut = CreateService();

        var result = await sut.ClassifyAsync(
            "All workers must wear PPE at all times",
            "construction", "pl");

        result.IsSafetyCritical.Should().BeTrue();
        result.CriticalTermsFound.Should().Contain("PPE");
        result.GlossaryMatches.Should().ContainSingle()
            .Which.ExpectedTranslation.Should().Be("ŚOI");
    }

    [Fact]
    public async Task ClassifyAsync_TextWithNoSafetyIndicators_NotSafetyCritical()
    {
        SetupGlossaryTerms([]);
        var sut = CreateService();

        var result = await sut.ClassifyAsync(
            "The meeting will start at nine o'clock in the conference room",
            "construction", "pl");

        result.IsSafetyCritical.Should().BeFalse();
        result.CriticalTermsFound.Should().BeEmpty();
    }

    [Fact]
    public async Task ClassifyAsync_EmptySectorKey_ReturnsEmptyResult_NoThrow()
    {
        SetupGlossaryTerms([]);
        var sut = CreateService();

        var result = await sut.ClassifyAsync(
            "Some normal text here",
            "", "pl");

        result.IsSafetyCritical.Should().BeFalse();
        result.CriticalTermsFound.Should().BeEmpty();
        result.GlossaryMatches.Should().BeEmpty();
    }

    [Fact]
    public async Task ClassifyAsync_GlossaryTerms_CaseInsensitiveMatching()
    {
        var glossary = new SafetyGlossary
        {
            Id = Guid.NewGuid(),
            SectorKey = "construction",
            SectorName = "Construction",
            IsActive = true
        };
        var term = new SafetyGlossaryTerm
        {
            Id = Guid.NewGuid(),
            GlossaryId = glossary.Id,
            Glossary = glossary,
            EnglishTerm = "harness",
            Category = "Equipment",
            IsCritical = true,
            Translations = """{"pl":"uprząż"}"""
        };
        SetupGlossaryTerms([term]);
        var sut = CreateService();

        var result = await sut.ClassifyAsync(
            "Workers must wear a HARNESS when working at height",
            "construction", "pl");

        result.IsSafetyCritical.Should().BeTrue();
        result.CriticalTermsFound.Should().Contain("harness");
    }
}

#region Async EF Core Test Helpers

internal class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;

    internal TestAsyncQueryProvider(IQueryProvider inner) => _inner = inner;

    public IQueryable CreateQuery(System.Linq.Expressions.Expression expression) =>
        new TestAsyncEnumerable<TEntity>(expression);

    public IQueryable<TElement> CreateQuery<TElement>(System.Linq.Expressions.Expression expression) =>
        new TestAsyncEnumerable<TElement>(expression);

    public object? Execute(System.Linq.Expressions.Expression expression) =>
        _inner.Execute(expression);

    public TResult Execute<TResult>(System.Linq.Expressions.Expression expression) =>
        _inner.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(System.Linq.Expressions.Expression expression, CancellationToken cancellationToken = default)
    {
        var expectedResultType = typeof(TResult).GetGenericArguments()[0];
        var executionResult = typeof(IQueryProvider)
            .GetMethod(
                name: nameof(IQueryProvider.Execute),
                genericParameterCount: 1,
                types: [typeof(System.Linq.Expressions.Expression)])!
            .MakeGenericMethod(expectedResultType)
            .Invoke(_inner, [expression]);

        return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(expectedResultType)
            .Invoke(null, [executionResult])!;
    }
}

internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
    public TestAsyncEnumerable(System.Linq.Expressions.Expression expression) : base(expression) { }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());

    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
}

internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _inner;

    public TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;

    public ValueTask DisposeAsync()
    {
        _inner.Dispose();
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(_inner.MoveNext());

    public T Current => _inner.Current;
}

#endregion
