using System.Text;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Features.BulkImport;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Core.Infrastructure.Services;
using QuantumBuild.Tests.Unit.ToolboxTalks.Regulatory;

namespace QuantumBuild.Tests.Unit.Core.BulkImport;

/// <summary>
/// Covers the CSV department string to tenant Department.Id matching added to
/// BulkEmployeeImportValidationService.ValidateAsync: case-insensitive exact match,
/// blank (no warning), and unmatched (warn and null, no auto-create).
/// </summary>
public class BulkEmployeeImportValidationServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly List<Department> _departments = new();
    private readonly List<Employee> _employees = new();
    private readonly List<User> _users = new();

    private BulkEmployeeImportValidationService CreateSut()
    {
        var contextMock = new Mock<ICoreDbContext>();
        contextMock.Setup(x => x.Departments).Returns(MockDbSetFactory.Create(_departments).Object);
        contextMock.Setup(x => x.Employees).Returns(MockDbSetFactory.Create(_employees).Object);
        contextMock.Setup(x => x.Users).Returns(MockDbSetFactory.Create(_users).Object);
        return new BulkEmployeeImportValidationService(contextMock.Object);
    }

    private Department AddDepartment(string name)
    {
        var department = new Department { Id = Guid.NewGuid(), TenantId = _tenantId, Name = name, IsActive = true };
        _departments.Add(department);
        return department;
    }

    private static Stream ToCsvStream(string csv) => new MemoryStream(Encoding.UTF8.GetBytes(csv));

    [Fact]
    public async Task ValidateAsync_DepartmentMatchesExistingByExactName_SetsDepartmentId()
    {
        var operations = AddDepartment("Operations");
        var sut = CreateSut();
        var csv = "FirstName,LastName,Email,CreateUserAccount,Department\n" +
                   "Alice,Smith,alice@example.com,No,Operations\n";

        var result = await sut.ValidateAsync(ToCsvStream(csv));

        var row = result.Rows.Single();
        row.Status.Should().Be(BulkImportRowStatus.Valid);
        row.DepartmentId.Should().Be(operations.Id);
        row.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_DepartmentMatchesExistingCaseInsensitively_SetsDepartmentId()
    {
        var operations = AddDepartment("Operations");
        var sut = CreateSut();
        var csv = "FirstName,LastName,Email,CreateUserAccount,Department\n" +
                   "Bob,Jones,bob@example.com,No,operations\n";

        var result = await sut.ValidateAsync(ToCsvStream(csv));

        var row = result.Rows.Single();
        row.Status.Should().Be(BulkImportRowStatus.Valid);
        row.DepartmentId.Should().Be(operations.Id);
    }

    [Fact]
    public async Task ValidateAsync_DepartmentBlank_LeavesDepartmentIdNullWithNoWarning()
    {
        AddDepartment("Operations");
        var sut = CreateSut();
        var csv = "FirstName,LastName,Email,CreateUserAccount,Department\n" +
                   "Carol,Lee,carol@example.com,No,\n";

        var result = await sut.ValidateAsync(ToCsvStream(csv));

        var row = result.Rows.Single();
        row.Status.Should().Be(BulkImportRowStatus.Valid);
        row.DepartmentId.Should().BeNull();
        row.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_DepartmentDoesNotMatchAny_WarnsAndLeavesDepartmentIdNull()
    {
        AddDepartment("Operations");
        var sut = CreateSut();
        var csv = "FirstName,LastName,Email,CreateUserAccount,Department\n" +
                   "Dave,Kim,dave@example.com,No,Warehouse\n";

        var result = await sut.ValidateAsync(ToCsvStream(csv));

        var row = result.Rows.Single();
        row.Status.Should().Be(BulkImportRowStatus.Warning);
        row.DepartmentId.Should().BeNull();
        row.Messages.Should().ContainSingle(m => m.Contains("Warehouse"));
        result.WarningRows.Should().Be(1);
        _departments.Should().HaveCount(1, "an unmatched department must not be auto-created");
    }

    [Fact]
    public async Task ValidateAsync_NoDepartmentsExistForTenant_EveryNonBlankValueWarnsAndNullsRatherThanFailing()
    {
        var sut = CreateSut();
        var csv = "FirstName,LastName,Email,CreateUserAccount,Department\n" +
                   "Erin,Ng,erin@example.com,No,Operations\n";

        var result = await sut.ValidateAsync(ToCsvStream(csv));

        var row = result.Rows.Single();
        row.Status.Should().Be(BulkImportRowStatus.Warning);
        row.DepartmentId.Should().BeNull();
    }
}
