namespace QuantumBuild.Tests.Unit.Builders;

/// <summary>
/// Tests for the EmployeeBuilder to ensure it creates valid entities.
/// </summary>
public class EmployeeBuilderTests
{
    [Fact]
    public void Build_WithDefaults_CreatesValidEmployee()
    {
        // Arrange & Act
        var employee = new EmployeeBuilder().Build();

        // Assert
        employee.Id.Should().NotBeEmpty();
        employee.TenantId.Should().Be(TestTenantConstants.TenantId);
        employee.FirstName.Should().NotBeNullOrEmpty();
        employee.LastName.Should().NotBeNullOrEmpty();
        employee.IsActive.Should().BeTrue();
        employee.PreferredLanguage.Should().Be("en");
    }

    [Fact]
    public void Build_WithName_SetsNameCorrectly()
    {
        // Arrange
        const string firstName = "John";
        const string lastName = "Doe";

        // Act
        var employee = new EmployeeBuilder()
            .WithName(firstName, lastName)
            .Build();

        // Assert
        employee.FirstName.Should().Be(firstName);
        employee.LastName.Should().Be(lastName);
        employee.Email.Should().Contain("john.doe");
    }

    [Fact]
    public void Build_AtSite_SetsPrimarySiteId()
    {
        // Arrange
        var siteId = TestTenantConstants.Sites.MainSite;

        // Act
        var employee = new EmployeeBuilder()
            .AtSite(siteId)
            .Build();

        // Assert
        employee.PrimarySiteId.Should().Be(siteId);
    }

    [Fact]
    public void Build_AsManager_SetsJobTitle()
    {
        // Act
        var employee = new EmployeeBuilder()
            .AsManager()
            .Build();

        // Assert
        employee.JobTitle.Should().Be("Site Manager");
    }

    [Fact]
    public void Build_AsInactive_SetsIsActiveFalse()
    {
        // Act
        var employee = new EmployeeBuilder()
            .AsInactive()
            .Build();

        // Assert
        employee.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Build_WithEndDate_SetsEndDateAndDeactivates()
    {
        // Arrange
        var endDate = DateTime.UtcNow.AddMonths(-1);

        // Act
        var employee = new EmployeeBuilder()
            .WithEndDate(endDate)
            .Build();

        // Assert
        employee.EndDate.Should().Be(endDate);
        employee.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Build_WithPreferredLanguage_SetsLanguageCode()
    {
        // Act
        var employee = new EmployeeBuilder()
            .WithPreferredLanguage("es")
            .Build();

        // Assert
        employee.PreferredLanguage.Should().Be("es");
    }

    [Fact]
    public void CreateActive_CreatesActiveEmployee()
    {
        // Act
        var employee = EmployeeBuilder.CreateActive("Jane", "Smith", TestTenantConstants.Sites.MainSite);

        // Assert
        employee.FirstName.Should().Be("Jane");
        employee.LastName.Should().Be("Smith");
        employee.PrimarySiteId.Should().Be(TestTenantConstants.Sites.MainSite);
        employee.IsActive.Should().BeTrue();
    }

    [Fact]
    public void CreateInactive_CreatesInactiveEmployee()
    {
        // Act
        var employee = EmployeeBuilder.CreateInactive("Former", "Employee");

        // Assert
        employee.FirstName.Should().Be("Former");
        employee.LastName.Should().Be("Employee");
        employee.IsActive.Should().BeFalse();
        employee.EndDate.Should().NotBeNull();
    }

    [Fact]
    public void CreateManager_CreatesManagerEmployee()
    {
        // Act
        var employee = EmployeeBuilder.CreateManager("Site", "Manager", TestTenantConstants.Sites.MainSite);

        // Assert
        employee.JobTitle.Should().Be("Site Manager");
        employee.PrimarySiteId.Should().Be(TestTenantConstants.Sites.MainSite);
    }

    [Fact]
    public void CreateWithLanguage_CreatesEmployeeWithSpecificLanguage()
    {
        // Act
        var employee = EmployeeBuilder.CreateWithLanguage("Polish", "Worker", "pl");

        // Assert
        employee.PreferredLanguage.Should().Be("pl");
    }
}
