namespace QuantumBuild.Tests.Integration.Core;

/// <summary>
/// Integration tests for Company CRUD operations.
/// </summary>
public class CompanyTests : IntegrationTestBase
{
    public CompanyTests(CustomWebApplicationFactory factory) : base(factory) { }

    #region Get Companies Tests

    [Fact]
    public async Task GetCompanies_ReturnsPagedResults()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/companies?pageNumber=1&pageSize=10");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<CompanyDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.PageNumber.Should().Be(1);
        result.Data.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetCompanies_WithSearch_ReturnsFilteredResults()
    {
        // Arrange - First create a company with a unique name
        var createCommand = new
        {
            CompanyCode = $"SRCH-{Guid.NewGuid():N}".Substring(0, 10),
            CompanyName = "UniqueSearchCompanyName",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/companies", createCommand);
        createResponse.EnsureSuccessStatusCode();

        // Act - Search for the unique name
        var response = await AdminClient.GetAsync("/api/companies?search=UniqueSearchCompanyName");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<CompanyDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Items.Should().Contain(c => c.CompanyName == "UniqueSearchCompanyName");
    }

    [Fact]
    public async Task GetCompanies_FilterByCompanyType_ReturnsFilteredResults()
    {
        // Arrange - First create a company with a specific type
        var createCommand = new
        {
            CompanyCode = $"TYPE-{Guid.NewGuid():N}".Substring(0, 10),
            CompanyName = "Typed Company",
            CompanyType = "Customer",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/companies", createCommand);
        createResponse.EnsureSuccessStatusCode();

        // Act - Filter by company type
        var response = await AdminClient.GetAsync("/api/companies?companyType=Customer");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<CompanyDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Items.Should().OnlyContain(c => c.CompanyType == "Customer" || c.CompanyType == null);
    }

    [Fact]
    public async Task GetAllCompanies_ReturnsNonPaginatedList()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/companies/all");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<List<CompanyDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCompanyById_ExistingCompany_ReturnsCompanyWithContacts()
    {
        // Arrange - First create a company
        var createCommand = new
        {
            CompanyCode = $"GET-{Guid.NewGuid():N}".Substring(0, 10),
            CompanyName = "GetById Company",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/companies", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<CompanyDto>>();
        var companyId = createResult!.Data!.Id;

        // Act
        var response = await AdminClient.GetAsync($"/api/companies/{companyId}");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<CompanyDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Id.Should().Be(companyId);
        result.Data.CompanyName.Should().Be("GetById Company");
        result.Data.Contacts.Should().NotBeNull(); // Contacts list should be included
    }

    [Fact]
    public async Task GetCompanyById_NonExistingCompany_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await AdminClient.GetAsync($"/api/companies/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Create Company Tests

    [Fact]
    public async Task CreateCompany_ValidData_ReturnsCreated()
    {
        // Arrange
        var command = new
        {
            CompanyCode = $"NEW-{Guid.NewGuid():N}".Substring(0, 10),
            CompanyName = "New Test Company",
            TradingName = "Test Co",
            RegistrationNumber = "12345678",
            VatNumber = "IE1234567T",
            AddressLine1 = "123 Business Street",
            AddressLine2 = "Suite 100",
            City = "Dublin",
            County = "Dublin",
            PostalCode = "D01 ABC1",
            Country = "Ireland",
            Phone = "01234567890",
            Email = $"company-{Guid.NewGuid():N}@test.quantumbuild.ie",
            Website = "https://testcompany.ie",
            CompanyType = "Customer",
            IsActive = true,
            Notes = "Test company created by integration test"
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/companies", command);
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<CompanyDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Id.Should().NotBeEmpty();
        result.Data.CompanyName.Should().Be("New Test Company");
        result.Data.TradingName.Should().Be("Test Co");
        result.Data.City.Should().Be("Dublin");
        result.Data.CompanyType.Should().Be("Customer");
    }

    [Fact]
    public async Task CreateCompany_MinimalData_ReturnsCreated()
    {
        // Arrange - Only required fields
        var command = new
        {
            CompanyCode = $"MIN-{Guid.NewGuid():N}".Substring(0, 10),
            CompanyName = "Minimal Company",
            IsActive = true
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/companies", command);
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<CompanyDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.CompanyName.Should().Be("Minimal Company");
    }

    [Fact]
    public async Task CreateCompany_MissingRequiredFields_ReturnsBadRequest()
    {
        // Arrange - Missing CompanyCode and CompanyName
        var command = new
        {
            City = "Dublin",
            IsActive = true
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/companies", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateCompany_DuplicateCompanyCode_ReturnsBadRequest()
    {
        // Arrange - First create a company
        var companyCode = $"DUP-{Guid.NewGuid():N}".Substring(0, 10);
        var command1 = new
        {
            CompanyCode = companyCode,
            CompanyName = "First Company",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/companies", command1);
        createResponse.EnsureSuccessStatusCode();

        // Try to create another company with the same code
        var command2 = new
        {
            CompanyCode = companyCode, // Same code
            CompanyName = "Second Company",
            IsActive = true
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/companies", command2);

        // Assert - Should fail due to duplicate code
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateCompany_Unauthenticated_Returns401()
    {
        // Arrange
        var command = new
        {
            CompanyCode = $"UNAUTH-{Guid.NewGuid():N}".Substring(0, 10),
            CompanyName = "Unauth Company",
            IsActive = true
        };

        // Act
        var response = await UnauthenticatedClient.PostAsJsonAsync("/api/companies", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateCompany_WithoutPermission_Returns403()
    {
        // Arrange
        var command = new
        {
            CompanyCode = $"NOPERM-{Guid.NewGuid():N}".Substring(0, 10),
            CompanyName = "No Permission Company",
            IsActive = true
        };

        // Act - Operator doesn't have ManageCompanies permission
        var response = await OperatorClient.PostAsJsonAsync("/api/companies", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Update Company Tests

    [Fact]
    public async Task UpdateCompany_ValidData_ReturnsOk()
    {
        // Arrange - First create a company
        var createCommand = new
        {
            CompanyCode = $"UPD-{Guid.NewGuid():N}".Substring(0, 10),
            CompanyName = "Original Company",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/companies", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<CompanyDto>>();
        var companyId = createResult!.Data!.Id;

        // Update the company
        var updateCommand = new
        {
            CompanyCode = createCommand.CompanyCode,
            CompanyName = "Updated Company Name",
            TradingName = "Updated Trading",
            City = "Cork",
            Phone = "0214567890",
            CompanyType = "Supplier",
            IsActive = true
        };

        // Act
        var response = await AdminClient.PutAsJsonAsync($"/api/companies/{companyId}", updateCommand);
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<CompanyDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.CompanyName.Should().Be("Updated Company Name");
        result.Data.TradingName.Should().Be("Updated Trading");
        result.Data.City.Should().Be("Cork");
        result.Data.CompanyType.Should().Be("Supplier");
    }

    [Fact]
    public async Task UpdateCompany_NonExisting_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var updateCommand = new
        {
            CompanyCode = "NONEXIST",
            CompanyName = "NonExistent Company",
            IsActive = true
        };

        // Act
        var response = await AdminClient.PutAsJsonAsync($"/api/companies/{nonExistentId}", updateCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateCompany_WithoutPermission_Returns403()
    {
        // Arrange - First create a company as Admin
        var createCommand = new
        {
            CompanyCode = $"PERM-{Guid.NewGuid():N}".Substring(0, 10),
            CompanyName = "Permission Test Company",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/companies", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<CompanyDto>>();
        var companyId = createResult!.Data!.Id;

        // Try to update as Operator
        var updateCommand = new
        {
            CompanyCode = createCommand.CompanyCode,
            CompanyName = "Modified By Operator",
            IsActive = true
        };

        // Act
        var response = await OperatorClient.PutAsJsonAsync($"/api/companies/{companyId}", updateCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Delete Company Tests

    [Fact]
    public async Task DeleteCompany_ExistingCompany_ReturnsNoContent()
    {
        // Arrange - Create company to delete
        var createCommand = new
        {
            CompanyCode = $"DEL-{Guid.NewGuid():N}".Substring(0, 10),
            CompanyName = "Company To Delete",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/companies", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<CompanyDto>>();
        var companyId = createResult!.Data!.Id;

        // Act
        var response = await AdminClient.DeleteAsync($"/api/companies/{companyId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deleted (soft delete) - should return NotFound
        var getResponse = await AdminClient.GetAsync($"/api/companies/{companyId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteCompany_NonExisting_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await AdminClient.DeleteAsync($"/api/companies/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteCompany_WithoutPermission_Returns403()
    {
        // Arrange - Create company as Admin
        var createCommand = new
        {
            CompanyCode = $"DPERM-{Guid.NewGuid():N}".Substring(0, 10),
            CompanyName = "Delete Permission Company",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/companies", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<CompanyDto>>();
        var companyId = createResult!.Data!.Id;

        // Act - Try to delete as Operator
        var response = await OperatorClient.DeleteAsync($"/api/companies/{companyId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Response DTOs

    private record ResultWrapper<T>(
        bool Success,
        T? Data,
        string? Message,
        List<string>? Errors
    );

    private record PaginatedResult<T>(
        List<T> Items,
        int PageNumber,
        int PageSize,
        int TotalCount,
        int TotalPages
    );

    private record ContactSummaryDto(
        Guid Id,
        string FirstName,
        string LastName,
        string FullName,
        string? JobTitle,
        string? Email,
        string? Phone,
        string? Mobile,
        bool IsPrimaryContact,
        bool IsActive
    );

    private record CompanyDto(
        Guid Id,
        string CompanyCode,
        string CompanyName,
        string? TradingName,
        string? RegistrationNumber,
        string? VatNumber,
        string? AddressLine1,
        string? AddressLine2,
        string? City,
        string? County,
        string? PostalCode,
        string? Country,
        string? Phone,
        string? Email,
        string? Website,
        string? CompanyType,
        bool IsActive,
        string? Notes,
        int ContactCount,
        List<ContactSummaryDto>? Contacts
    );

    #endregion
}
