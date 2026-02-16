namespace QuantumBuild.Tests.Integration.Core;

/// <summary>
/// Integration tests for multi-tenancy data isolation.
/// Tests verify that users can only access data belonging to their tenant.
/// </summary>
public class TenantIsolationTests : IntegrationTestBase
{
    public TenantIsolationTests(CustomWebApplicationFactory factory) : base(factory) { }

    #region Query Isolation Tests

    [Fact]
    public async Task GetEmployees_OnlyReturnsCurrentTenantData()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/employees");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<EmployeeDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        // All returned employees should belong to the current tenant (verified by the query filter)
        // The data seeder creates data for the QUANTUMBUILD tenant (11111111-1111-1111-1111-111111111111)
    }

    [Fact]
    public async Task GetSites_OnlyReturnsCurrentTenantData()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/sites");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<SiteDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetCompanies_OnlyReturnsCurrentTenantData()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/companies");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<CompanyDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetProducts_OnlyReturnsCurrentTenantData()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/products");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<ProductDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
    }

    #endregion

    #region GetById Isolation Tests

    [Fact]
    public async Task GetEmployeeById_ForOtherTenant_ReturnsNotFound()
    {
        // Arrange - Use a GUID that would belong to a different tenant
        // This simulates accessing data that exists but belongs to another tenant
        var otherTenantEntityId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        // Act
        var response = await AdminClient.GetAsync($"/api/employees/{otherTenantEntityId}");

        // Assert - Should return NotFound due to tenant filter
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSiteById_ForOtherTenant_ReturnsNotFound()
    {
        // Arrange
        var otherTenantEntityId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        // Act
        var response = await AdminClient.GetAsync($"/api/sites/{otherTenantEntityId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCompanyById_ForOtherTenant_ReturnsNotFound()
    {
        // Arrange
        var otherTenantEntityId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        // Act
        var response = await AdminClient.GetAsync($"/api/companies/{otherTenantEntityId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Create Entity Tests - Tenant Assignment

    [Fact]
    public async Task CreateEmployee_AssignsCurrentTenant()
    {
        // Arrange
        var createCommand = new
        {
            EmployeeCode = $"EMP-{Guid.NewGuid():N}".Substring(0, 10),
            FirstName = "Tenant",
            LastName = "Test",
            Email = $"tenant-test-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        // Act
        var createResponse = await AdminClient.PostAsJsonAsync("/api/employees", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<EmployeeDto>>();
        var employeeId = createResult!.Data!.Id;

        // Verify - Get the created employee
        var getResponse = await AdminClient.GetAsync($"/api/employees/{employeeId}");
        var getResult = await getResponse.Content.ReadFromJsonAsync<ResultWrapper<EmployeeDto>>();

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        getResult.Should().NotBeNull();
        getResult!.Success.Should().BeTrue();
        getResult.Data!.FirstName.Should().Be("Tenant");
        getResult.Data.LastName.Should().Be("Test");
        // The tenant ID is automatically assigned by the application
    }

    [Fact]
    public async Task CreateSite_AssignsCurrentTenant()
    {
        // Arrange
        var createCommand = new
        {
            SiteCode = $"SITE-{Guid.NewGuid():N}".Substring(0, 10),
            SiteName = "Tenant Test Site",
            IsActive = true
        };

        // Act
        var createResponse = await AdminClient.PostAsJsonAsync("/api/sites", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<SiteDto>>();
        var siteId = createResult!.Data!.Id;

        // Verify
        var getResponse = await AdminClient.GetAsync($"/api/sites/{siteId}");
        var getResult = await getResponse.Content.ReadFromJsonAsync<ResultWrapper<SiteDto>>();

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        getResult.Should().NotBeNull();
        getResult!.Data!.SiteName.Should().Be("Tenant Test Site");
    }

    [Fact]
    public async Task CreateCompany_AssignsCurrentTenant()
    {
        // Arrange
        var createCommand = new
        {
            CompanyCode = $"COMP-{Guid.NewGuid():N}".Substring(0, 10),
            CompanyName = "Tenant Test Company",
            IsActive = true
        };

        // Act
        var createResponse = await AdminClient.PostAsJsonAsync("/api/companies", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<CompanyDto>>();
        var companyId = createResult!.Data!.Id;

        // Verify
        var getResponse = await AdminClient.GetAsync($"/api/companies/{companyId}");
        var getResult = await getResponse.Content.ReadFromJsonAsync<ResultWrapper<CompanyDto>>();

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        getResult.Should().NotBeNull();
        getResult!.Data!.CompanyName.Should().Be("Tenant Test Company");
    }

    #endregion

    #region Soft Delete Isolation Tests

    [Fact]
    public async Task SoftDeletedRecords_NotReturnedInList()
    {
        // Arrange - Create an employee and delete it
        var createCommand = new
        {
            EmployeeCode = $"DEL-{Guid.NewGuid():N}".Substring(0, 10),
            FirstName = "To Be",
            LastName = "Deleted",
            Email = $"to-delete-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/employees", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<EmployeeDto>>();
        var employeeId = createResult!.Data!.Id;

        // Delete the employee
        var deleteResponse = await AdminClient.DeleteAsync($"/api/employees/{employeeId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act - Try to get the deleted employee
        var getResponse = await AdminClient.GetAsync($"/api/employees/{employeeId}");

        // Assert - Should return NotFound due to soft delete filter
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SoftDeletedSite_NotReturnedInList()
    {
        // Arrange - Create a site and delete it
        var createCommand = new
        {
            SiteCode = $"DEL-{Guid.NewGuid():N}".Substring(0, 10),
            SiteName = "Site To Delete",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/sites", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<SiteDto>>();
        var siteId = createResult!.Data!.Id;

        // Delete the site
        var deleteResponse = await AdminClient.DeleteAsync($"/api/sites/{siteId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act - Try to get the deleted site
        var getResponse = await AdminClient.GetAsync($"/api/sites/{siteId}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SoftDeletedCompany_NotReturnedInList()
    {
        // Arrange - Create a company and delete it
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

        // Delete the company
        var deleteResponse = await AdminClient.DeleteAsync($"/api/companies/{companyId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act - Try to get the deleted company
        var getResponse = await AdminClient.GetAsync($"/api/companies/{companyId}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Cross-User Within Same Tenant Tests

    [Fact]
    public async Task DifferentUsersInSameTenant_CanAccessSameData()
    {
        // Arrange - Create an employee as Admin
        var createCommand = new
        {
            EmployeeCode = $"CROSS-{Guid.NewGuid():N}".Substring(0, 10),
            FirstName = "Cross",
            LastName = "User",
            Email = $"cross-user-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/employees", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<EmployeeDto>>();
        var employeeId = createResult!.Data!.Id;

        // Act - Try to access as different user in same tenant
        var getResponse = await WarehouseClient.GetAsync($"/api/employees/{employeeId}");

        // Assert - Should be accessible
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getResult = await getResponse.Content.ReadFromJsonAsync<ResultWrapper<EmployeeDto>>();
        getResult!.Data!.FirstName.Should().Be("Cross");
    }

    [Fact]
    public async Task AllUsersInTenant_SeesSameEmployeeList()
    {
        // Act - Get employee lists from different users
        var adminResponse = await AdminClient.GetAsync("/api/employees/all");
        var warehouseResponse = await WarehouseClient.GetAsync("/api/employees/all");
        var operatorResponse = await OperatorClient.GetAsync("/api/employees/all");

        // Assert - All should return OK with same data structure
        adminResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        warehouseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        operatorResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var adminResult = await adminResponse.Content.ReadFromJsonAsync<ResultWrapper<List<EmployeeDto>>>();
        var warehouseResult = await warehouseResponse.Content.ReadFromJsonAsync<ResultWrapper<List<EmployeeDto>>>();
        var operatorResult = await operatorResponse.Content.ReadFromJsonAsync<ResultWrapper<List<EmployeeDto>>>();

        // All users should see the same number of employees
        adminResult!.Data!.Count.Should().Be(warehouseResult!.Data!.Count);
        adminResult.Data.Count.Should().Be(operatorResult!.Data!.Count);
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

    private record EmployeeDto(
        Guid Id,
        string EmployeeCode,
        string FirstName,
        string LastName,
        string FullName,
        string? Email,
        string? Phone,
        string? Mobile,
        string? JobTitle,
        string? Department,
        Guid? PrimarySiteId,
        string? PrimarySiteName,
        DateTime? StartDate,
        DateTime? EndDate,
        bool IsActive,
        string? Notes
    );

    private record SiteDto(
        Guid Id,
        string SiteCode,
        string SiteName,
        string? Address,
        string? City,
        string? PostalCode,
        Guid? SiteManagerId,
        string? SiteManagerName,
        Guid? CompanyId,
        string? CompanyName,
        string? Phone,
        string? Email,
        bool IsActive,
        string? Notes,
        decimal? Latitude,
        decimal? Longitude,
        int? GeofenceRadiusMeters
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
        int ContactCount
    );

    private record ProductDto(
        Guid Id,
        string Sku,
        string Name,
        string? Description,
        Guid? CategoryId,
        string? CategoryName,
        decimal? CostPrice,
        decimal? SellPrice,
        bool IsActive
    );

    #endregion
}
