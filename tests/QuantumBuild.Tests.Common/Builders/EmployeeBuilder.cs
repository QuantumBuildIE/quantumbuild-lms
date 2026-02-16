using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Tests.Common.Builders;

/// <summary>
/// Fluent builder for creating Employee entities in tests.
/// </summary>
public class EmployeeBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _tenantId = TestTenant.TestTenantConstants.TenantId;
    private string _employeeCode = $"EMP-{Guid.NewGuid().ToString()[..8]}";
    private string _firstName = "Test";
    private string _lastName = "Employee";
    private string? _email = null;
    private string? _phone = null;
    private string? _mobile = null;
    private string? _jobTitle = "Site Worker";
    private string? _department = null;
    private string? _userId = null;
    private Guid? _primarySiteId = null;
    private bool _isActive = true;
    private DateTime? _startDate = null;
    private DateTime? _endDate = null;
    private string? _notes = null;
    private string _preferredLanguage = "en";

    public EmployeeBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public EmployeeBuilder WithTenantId(Guid tenantId)
    {
        _tenantId = tenantId;
        return this;
    }

    public EmployeeBuilder WithCode(string code)
    {
        _employeeCode = code;
        return this;
    }

    public EmployeeBuilder WithName(string firstName, string lastName)
    {
        _firstName = firstName;
        _lastName = lastName;
        return this;
    }

    public EmployeeBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public EmployeeBuilder WithPhone(string phone)
    {
        _phone = phone;
        return this;
    }

    public EmployeeBuilder WithMobile(string mobile)
    {
        _mobile = mobile;
        return this;
    }

    public EmployeeBuilder WithJobTitle(string jobTitle)
    {
        _jobTitle = jobTitle;
        return this;
    }

    public EmployeeBuilder WithDepartment(string department)
    {
        _department = department;
        return this;
    }

    public EmployeeBuilder LinkedToUser(string userId)
    {
        _userId = userId;
        return this;
    }

    public EmployeeBuilder AtSite(Guid siteId)
    {
        _primarySiteId = siteId;
        return this;
    }

    public EmployeeBuilder WithStartDate(DateTime startDate)
    {
        _startDate = startDate;
        return this;
    }

    public EmployeeBuilder WithEndDate(DateTime endDate)
    {
        _endDate = endDate;
        _isActive = false;
        return this;
    }

    public EmployeeBuilder WithNotes(string notes)
    {
        _notes = notes;
        return this;
    }

    public EmployeeBuilder WithPreferredLanguage(string languageCode)
    {
        _preferredLanguage = languageCode;
        return this;
    }

    public EmployeeBuilder AsInactive()
    {
        _isActive = false;
        return this;
    }

    public EmployeeBuilder AsManager()
    {
        _jobTitle = "Site Manager";
        return this;
    }

    public EmployeeBuilder AsOperator()
    {
        _jobTitle = "Machine Operator";
        return this;
    }

    public Employee Build()
    {
        return new Employee
        {
            Id = _id,
            TenantId = _tenantId,
            EmployeeCode = _employeeCode,
            FirstName = _firstName,
            LastName = _lastName,
            Email = _email ?? $"{_firstName.ToLower()}.{_lastName.ToLower()}@test.quantumbuild.ie",
            Phone = _phone,
            Mobile = _mobile,
            JobTitle = _jobTitle,
            Department = _department,
            UserId = _userId,
            PrimarySiteId = _primarySiteId,
            IsActive = _isActive,
            StartDate = _startDate ?? DateTime.UtcNow.AddYears(-1),
            EndDate = _endDate,
            Notes = _notes,
            PreferredLanguage = _preferredLanguage,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test-builder"
        };
    }

    /// <summary>
    /// Creates a simple active employee.
    /// </summary>
    public static Employee CreateActive(string firstName, string lastName, Guid? siteId = null, Guid? id = null)
    {
        var builder = new EmployeeBuilder()
            .WithId(id ?? Guid.NewGuid())
            .WithName(firstName, lastName);

        if (siteId.HasValue)
            builder.AtSite(siteId.Value);

        return builder.Build();
    }

    /// <summary>
    /// Creates an inactive (terminated) employee.
    /// </summary>
    public static Employee CreateInactive(string firstName, string lastName, Guid? id = null)
    {
        return new EmployeeBuilder()
            .WithId(id ?? Guid.NewGuid())
            .WithName(firstName, lastName)
            .WithEndDate(DateTime.UtcNow.AddMonths(-1))
            .Build();
    }

    /// <summary>
    /// Creates a site manager employee.
    /// </summary>
    public static Employee CreateManager(string firstName, string lastName, Guid siteId, Guid? id = null)
    {
        return new EmployeeBuilder()
            .WithId(id ?? Guid.NewGuid())
            .WithName(firstName, lastName)
            .AtSite(siteId)
            .AsManager()
            .Build();
    }

    /// <summary>
    /// Creates an employee with a specific language preference.
    /// </summary>
    public static Employee CreateWithLanguage(string firstName, string lastName, string languageCode, Guid? id = null)
    {
        return new EmployeeBuilder()
            .WithId(id ?? Guid.NewGuid())
            .WithName(firstName, lastName)
            .WithPreferredLanguage(languageCode)
            .Build();
    }
}
