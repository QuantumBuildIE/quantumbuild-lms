using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;
using QuantumBuild.Tests.Unit.ToolboxTalks.Regulatory;

namespace QuantumBuild.Tests.Unit.ToolboxTalks.Validation;

/// <summary>
/// Characterisation tests for the glossary term counts on the pipeline audit dashboard:
/// system terms vs tenant overrides, both limited to the tenant's sectors.
/// </summary>
public class PipelineAuditDashboardGlossaryCountTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private readonly Mock<IToolboxTalksDbContext> _dbContext = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private readonly List<SafetyGlossary> _glossaries = [];
    private readonly List<SafetyGlossaryTerm> _terms = [];
    private readonly List<TenantSector> _tenantSectors = [];

    private PipelineAuditQueryService CreateService()
    {
        _dbContext.Setup(d => d.SafetyGlossaries).Returns(MockDbSetFactory.Create(_glossaries).Object);
        _dbContext.Setup(d => d.SafetyGlossaryTerms).Returns(MockDbSetFactory.Create(_terms).Object);
        _dbContext.Setup(d => d.TenantSectors).Returns(MockDbSetFactory.Create(_tenantSectors).Object);
        return new PipelineAuditQueryService(_dbContext.Object, _currentUser.Object);
    }

    private void AddTenantSector(Guid tenantId, string sectorKey) =>
        _tenantSectors.Add(new TenantSector
        {
            TenantId = tenantId,
            Sector = new Sector { Key = sectorKey }
        });

    private void AddGlossary(Guid? tenantId, string sectorKey, int termCount)
    {
        var glossary = new SafetyGlossary { Id = Guid.NewGuid(), TenantId = tenantId, SectorKey = sectorKey };
        _glossaries.Add(glossary);
        for (var i = 0; i < termCount; i++)
            _terms.Add(new SafetyGlossaryTerm { Id = Guid.NewGuid(), GlossaryId = glossary.Id, EnglishTerm = $"term-{i}" });
    }

    [Fact]
    public async Task TenantWithNoOverrides_CountsSystemTermsOnly()
    {
        AddTenantSector(TenantA, "construction");
        AddGlossary(null, "construction", 5);

        var dto = await CreateService().GetGlossaryTermCountsAsync(TenantA, default);

        dto.System.Should().Be(5);
        dto.Override.Should().Be(0);
    }

    [Fact]
    public async Task TenantOverrides_AreCounted_OtherTenantsOverridesExcluded()
    {
        AddTenantSector(TenantA, "construction");
        AddTenantSector(TenantB, "construction");
        AddGlossary(null, "construction", 5);
        AddGlossary(TenantA, "construction", 2);
        AddGlossary(TenantB, "construction", 7);

        var dto = await CreateService().GetGlossaryTermCountsAsync(TenantA, default);

        dto.System.Should().Be(5);
        dto.Override.Should().Be(2);
    }

    [Fact]
    public async Task TermsInSectorsTenantDoesNotHave_AreExcluded()
    {
        AddTenantSector(TenantA, "construction");
        AddGlossary(null, "construction", 5);
        AddGlossary(null, "healthcare", 9);
        AddGlossary(TenantA, "construction", 2);
        AddGlossary(TenantA, "healthcare", 4);

        var dto = await CreateService().GetGlossaryTermCountsAsync(TenantA, default);

        dto.System.Should().Be(5);
        dto.Override.Should().Be(2);
    }

    [Fact]
    public async Task NoTenant_SuperUser_CountsAllSectorsAndAllTenantsOverrides()
    {
        AddGlossary(null, "construction", 5);
        AddGlossary(null, "healthcare", 9);
        AddGlossary(TenantA, "construction", 2);
        AddGlossary(TenantB, "healthcare", 7);

        var dto = await CreateService().GetGlossaryTermCountsAsync(null, default);

        dto.System.Should().Be(14);
        dto.Override.Should().Be(9);
    }
}
