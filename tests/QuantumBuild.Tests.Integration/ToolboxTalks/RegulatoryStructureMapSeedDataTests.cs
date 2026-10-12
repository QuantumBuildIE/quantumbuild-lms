using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Seed;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Regulatory.StructureMaps;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Characterises the generalised structure-map seeder: binds only on exact (body code, document
/// title); zero matches -> Warning, no map; multiple -> Error, no map; HIQA's existing map is never
/// touched. Uses unique body codes so tests do not depend on what startup seeding left behind.
/// </summary>
[Collection("Integration")]
public class RegulatoryStructureMapSeedDataTests : IntegrationTestBase
{
    public RegulatoryStructureMapSeedDataTests(CustomWebApplicationFactory factory) : base(factory) { }

    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception)));
    }

    private static string UniqueCode() => $"SM{Guid.NewGuid():N}"[..12];

    private static StructureMapSeedSource TinySource(string bodyCode, string title) =>
        new(bodyCode, title, new[]
        {
            new StructurePrinciple(1, new[]
            {
                new StructureStandard("1.1", new[]
                {
                    new StructureFeature("1.1.1", RequirementBlock.Provider, "Tiny feature one."),
                    new StructureFeature("1.1.2", RequirementBlock.Provider, "Tiny feature two.", "A footnote."),
                }),
            }),
        });

    private static async Task<RegulatoryBody> AddBodyAsync(DbContext context, string code)
    {
        var body = new RegulatoryBody { Id = Guid.NewGuid(), Name = "Seed Test Body", Code = code, Country = "IE" };
        context.Set<RegulatoryBody>().Add(body);
        await context.SaveChangesAsync();
        return body;
    }

    private static async Task<RegulatoryDocument> AddDocumentAsync(DbContext context, Guid bodyId, string title)
    {
        var doc = new RegulatoryDocument { Id = Guid.NewGuid(), RegulatoryBodyId = bodyId, Title = title, Version = "1.0" };
        context.Set<RegulatoryDocument>().Add(doc);
        await context.SaveChangesAsync();
        return doc;
    }

    private static Task<List<RegulatoryStructureMap>> MapsForAsync(DbContext context, Guid documentId) =>
        context.Set<RegulatoryStructureMap>().IgnoreQueryFilters()
            .Where(m => m.RegulatoryDocumentId == documentId && !m.IsDeleted)
            .Include(m => m.Principles).ThenInclude(p => p.Standards).ThenInclude(s => s.Features)
            .ToListAsync();

    // ── Title binding ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OneExactTitleMatch_BindsAndSeedsDraft()
    {
        var context = GetDbContext();
        var code = UniqueCode();
        var body = await AddBodyAsync(context, code);
        var target = await AddDocumentAsync(context, body.Id, "Exact Title");
        var logger = new CapturingLogger();

        await RegulatoryStructureMapSeedData.SeedAsync(context, logger, new[] { TinySource(code, "Exact Title") });

        var maps = await MapsForAsync(context, target.Id);
        maps.Should().ContainSingle();
        maps[0].Status.Should().Be(RegulatoryStructureMapStatus.Draft);
        maps[0].Principles.SelectMany(p => p.Standards).SelectMany(s => s.Features).Should().HaveCount(2);
        logger.Entries.Should().NotContain(e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task ZeroMatches_NoMap_LogsWarning()
    {
        var context = GetDbContext();
        var code = UniqueCode();
        var body = await AddBodyAsync(context, code);
        var other = await AddDocumentAsync(context, body.Id, "Some Other Title");
        var logger = new CapturingLogger();

        await RegulatoryStructureMapSeedData.SeedAsync(context, logger, new[] { TinySource(code, "Exact Title") });

        (await MapsForAsync(context, other.Id)).Should().BeEmpty();
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Message.Contains("not bound"));
        logger.Entries.Should().NotContain(e => e.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task TwoMatches_NoMap_LogsError()
    {
        var context = GetDbContext();
        var code = UniqueCode();
        var body = await AddBodyAsync(context, code);
        var first = await AddDocumentAsync(context, body.Id, "Exact Title");
        var second = await AddDocumentAsync(context, body.Id, "Exact Title");
        var logger = new CapturingLogger();

        await RegulatoryStructureMapSeedData.SeedAsync(context, logger, new[] { TinySource(code, "Exact Title") });

        (await MapsForAsync(context, first.Id)).Should().BeEmpty();
        (await MapsForAsync(context, second.Id)).Should().BeEmpty();
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Message.Contains("ambiguous"));
    }

    [Fact]
    public async Task DifferentTitleInSameBody_IsNotBound_WhileExactMatchIs()
    {
        var context = GetDbContext();
        var code = UniqueCode();
        var body = await AddBodyAsync(context, code);
        var decoy = await AddDocumentAsync(context, body.Id, "Exact Title (superseded)");
        var target = await AddDocumentAsync(context, body.Id, "Exact Title");

        await RegulatoryStructureMapSeedData.SeedAsync(context, new CapturingLogger(), new[] { TinySource(code, "Exact Title") });

        (await MapsForAsync(context, decoy.Id)).Should().BeEmpty();
        (await MapsForAsync(context, target.Id)).Should().ContainSingle();
    }

    [Fact]
    public async Task MissingBody_LogsWarning_AndDoesNotThrow()
    {
        var context = GetDbContext();
        var logger = new CapturingLogger();

        await RegulatoryStructureMapSeedData.SeedAsync(context, logger, new[] { TinySource(UniqueCode(), "Exact Title") });

        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Message.Contains("body not found"));
    }

    [Fact]
    public async Task Seeding_IsIdempotent_AndNeverOverwritesExistingMap()
    {
        var context = GetDbContext();
        var code = UniqueCode();
        var body = await AddBodyAsync(context, code);
        var target = await AddDocumentAsync(context, body.Id, "Exact Title");
        var source = TinySource(code, "Exact Title");

        await RegulatoryStructureMapSeedData.SeedAsync(context, new CapturingLogger(), new[] { source });
        var firstMap = (await MapsForAsync(context, target.Id)).Single();
        var feature = firstMap.Principles.SelectMany(p => p.Standards).SelectMany(s => s.Features).First();
        feature.VerbatimText = "Human-edited text.";
        await context.SaveChangesAsync();

        await RegulatoryStructureMapSeedData.SeedAsync(context, new CapturingLogger(), new[] { source });

        var maps = await MapsForAsync(context, target.Id);
        maps.Should().ContainSingle().Which.Id.Should().Be(firstMap.Id);
        maps[0].Principles.SelectMany(p => p.Standards).SelectMany(s => s.Features)
            .Should().Contain(f => f.VerbatimText == "Human-edited text.");
    }

    // ── HIQA unchanged ────────────────────────────────────────────────────────────────────

    [Fact]
    public void HiqaSource_DeclaresBodyAndTitleMatchingTheSeededDocument()
    {
        HiqaStructureMap.Source.RegulatoryBodyCode.Should().Be("HIQA");
        HiqaStructureMap.Source.DocumentTitle.Should().Be("Draft National Standards for Home Support Services");
        RegulatoryStructureMapSeedData.Sources.Select(s => s.RegulatoryBodyCode).Should().Equal("HIQA", "RSA");
    }

    [Fact]
    public async Task HiqaExistingMap_IsLeftUntouched_ByTheFullSeeder()
    {
        var context = GetDbContext();

        var body = await context.Set<RegulatoryBody>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => !b.IsDeleted && b.Code == HiqaStructureMap.RegulatoryBodyCode)
            ?? await AddBodyAsync(context, HiqaStructureMap.RegulatoryBodyCode);
        var doc = await context.Set<RegulatoryDocument>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => !d.IsDeleted && d.RegulatoryBodyId == body.Id && d.Title == HiqaStructureMap.DocumentTitle)
            ?? await AddDocumentAsync(context, body.Id, HiqaStructureMap.DocumentTitle);

        if ((await MapsForAsync(context, doc.Id)).Count == 0)
            await RegulatoryStructureMapSeedData.SeedAsync(context, new CapturingLogger(), new[] { HiqaStructureMap.Source });

        var before = (await MapsForAsync(context, doc.Id)).Single();
        var beforeFeatures = before.Principles.SelectMany(p => p.Standards).SelectMany(s => s.Features)
            .Select(f => (f.Id, f.Identifier, f.Block, f.VerbatimText)).OrderBy(f => f.Id).ToList();
        var beforeStatus = before.Status;
        var beforeUpdatedAt = before.UpdatedAt;

        await RegulatoryStructureMapSeedData.SeedAsync(context, new CapturingLogger());

        var after = (await MapsForAsync(context, doc.Id)).Single();
        after.Id.Should().Be(before.Id);
        after.Status.Should().Be(beforeStatus);
        after.UpdatedAt.Should().Be(beforeUpdatedAt);
        after.Principles.SelectMany(p => p.Standards).SelectMany(s => s.Features)
            .Select(f => (f.Id, f.Identifier, f.Block, f.VerbatimText)).OrderBy(f => f.Id)
            .Should().Equal(beforeFeatures);
    }

    // ── RSA persisted shape ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task RsaSource_SeedsDraftMapWithApprovedCounts_AndFootnote()
    {
        var context = GetDbContext();
        var code = UniqueCode();
        var body = await AddBodyAsync(context, code);
        var target = await AddDocumentAsync(context, body.Id, RsaStructureMap.DocumentTitle);

        await RegulatoryStructureMapSeedData.SeedAsync(context, new CapturingLogger(),
            new[] { new StructureMapSeedSource(code, RsaStructureMap.DocumentTitle, RsaStructureMap.Principles) });

        var map = (await MapsForAsync(context, target.Id)).Single();
        map.Status.Should().Be(RegulatoryStructureMapStatus.Draft);
        map.Principles.Should().HaveCount(4);
        map.Principles.SelectMany(p => p.Standards).Should().HaveCount(9);
        var features = map.Principles.SelectMany(p => p.Standards).SelectMany(s => s.Features).ToList();
        features.Should().HaveCount(40);
        features.Should().OnlyContain(f => f.Block == RequirementBlock.Provider);
        features.Single(f => f.Identifier == "6(5)").FootnoteDefinition.Should().Contain("165/2014");
    }
}
