namespace QuantumBuild.Tests.Integration.Core;

/// <summary>
/// Integration tests for Employee CRUD operations.
/// </summary>
public class EmployeeTests : IntegrationTestBase
{
    public EmployeeTests(CustomWebApplicationFactory factory) : base(factory) { }

    #region Get Employees Tests

    [Fact]
    public async Task GetEmployees_ReturnsPagedResults()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/employees?pageNumber=1&pageSize=10");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<EmployeeDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.PageNumber.Should().Be(1);
        result.Data.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetEmployees_WithSearch_ReturnsFilteredResults()
    {
        // Arrange - First create an employee with a unique name
        var createCommand = new
        {
            EmployeeCode = $"SRCH-{Guid.NewGuid():N}".Substring(0, 10),
            FirstName = "UniqueSearchName",
            LastName = "Employee",
            Email = $"search-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/employees", createCommand);
        createResponse.EnsureSuccessStatusCode();

        // Act - Search for the unique name
        var response = await AdminClient.GetAsync("/api/employees?search=UniqueSearchName");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<EmployeeDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Items.Should().Contain(e => e.FirstName == "UniqueSearchName");
    }

    [Fact]
    public async Task GetAllEmployees_ReturnsNonPaginatedList()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/employees/all");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<List<EmployeeDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetEmployeeById_ExistingEmployee_ReturnsEmployee()
    {
        // Arrange - First create an employee
        var createCommand = new
        {
            EmployeeCode = $"GET-{Guid.NewGuid():N}".Substring(0, 10),
            FirstName = "GetById",
            LastName = "Test",
            Email = $"getbyid-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/employees", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<EmployeeDto>>();
        var employeeId = createResult!.Data!.Id;

        // Act
        var response = await AdminClient.GetAsync($"/api/employees/{employeeId}");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<EmployeeDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Id.Should().Be(employeeId);
        result.Data.FirstName.Should().Be("GetById");
    }

    [Fact]
    public async Task GetEmployeeById_NonExistingEmployee_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await AdminClient.GetAsync($"/api/employees/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Create Employee Tests

    [Fact]
    public async Task CreateEmployee_ValidData_ReturnsCreated()
    {
        // Arrange
        var command = new
        {
            EmployeeCode = $"NEW-{Guid.NewGuid():N}".Substring(0, 10),
            FirstName = "New",
            LastName = "Employee",
            Email = $"new-employee-{Guid.NewGuid():N}@test.quantumbuild.ie",
            Phone = "01234567890",
            Mobile = "0871234567",
            JobTitle = "Test Position",
            Department = "Test Department",
            IsActive = true,
            Notes = "Test employee created by integration test"
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/employees", command);
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<EmployeeDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Id.Should().NotBeEmpty();
        result.Data.FirstName.Should().Be("New");
        result.Data.LastName.Should().Be("Employee");
        result.Data.JobTitle.Should().Be("Test Position");
    }

    [Fact]
    public async Task CreateEmployee_WithSite_ReturnsCreatedWithSite()
    {
        // Arrange - First get an existing site
        var sitesResponse = await AdminClient.GetAsync("/api/sites/all");
        var sitesResult = await sitesResponse.Content.ReadFromJsonAsync<ResultWrapper<List<SiteDto>>>();
        var site = sitesResult!.Data!.FirstOrDefault();

        var command = new
        {
            EmployeeCode = $"SITE-{Guid.NewGuid():N}".Substring(0, 10),
            FirstName = "Site",
            LastName = "Employee",
            Email = $"site-employee-{Guid.NewGuid():N}@test.quantumbuild.ie",
            PrimarySiteId = site?.Id,
            IsActive = true
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/employees", command);
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<EmployeeDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        if (site != null)
        {
            result.Data!.PrimarySiteId.Should().Be(site.Id);
        }
    }

    [Fact]
    public async Task CreateEmployee_MissingRequiredFields_ReturnsBadRequest()
    {
        // Arrange - Missing required fields
        var command = new
        {
            FirstName = "Missing",
            // Missing EmployeeCode and LastName
            IsActive = true
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/employees", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateEmployee_AutoGeneratesUniqueEmployeeCode()
    {
        // Arrange - Create two employees, both sending the same EmployeeCode in request body
        // The backend should ignore the sent code and auto-generate unique ones
        var sameCodeInRequest = "IGNORED";
        var command1 = new
        {
            EmployeeCode = sameCodeInRequest,
            FirstName = "First",
            LastName = "Employee",
            Email = $"first-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        // Act - Create first employee
        var response1 = await AdminClient.PostAsJsonAsync("/api/employees", command1);
        var result1 = await response1.Content.ReadFromJsonAsync<ResultWrapper<EmployeeDto>>();

        // Assert - First employee created successfully
        response1.StatusCode.Should().Be(HttpStatusCode.Created);
        result1.Should().NotBeNull();
        result1!.Success.Should().BeTrue();
        result1.Data.Should().NotBeNull();

        // Create second employee with the same code in request body
        var command2 = new
        {
            EmployeeCode = sameCodeInRequest, // Same code sent - should be ignored
            FirstName = "Second",
            LastName = "Employee",
            Email = $"second-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        // Act - Create second employee
        var response2 = await AdminClient.PostAsJsonAsync("/api/employees", command2);
        var result2 = await response2.Content.ReadFromJsonAsync<ResultWrapper<EmployeeDto>>();

        // Assert - Second employee also created successfully (backend ignores sent code)
        response2.StatusCode.Should().Be(HttpStatusCode.Created);
        result2.Should().NotBeNull();
        result2!.Success.Should().BeTrue();
        result2.Data.Should().NotBeNull();

        // Assert - Both employees have different auto-generated EmployeeCodes
        var employeeCode1 = result1.Data!.EmployeeCode;
        var employeeCode2 = result2.Data!.EmployeeCode;

        employeeCode1.Should().NotBe(sameCodeInRequest, "backend should ignore sent EmployeeCode");
        employeeCode2.Should().NotBe(sameCodeInRequest, "backend should ignore sent EmployeeCode");
        employeeCode1.Should().NotBe(employeeCode2, "each employee should have a unique code");

        // Assert - Both codes follow the EMP### pattern
        employeeCode1.Should().MatchRegex(@"^EMP\d{3}$", "EmployeeCode should follow EMP### pattern");
        employeeCode2.Should().MatchRegex(@"^EMP\d{3}$", "EmployeeCode should follow EMP### pattern");
    }

    [Fact]
    public async Task CreateEmployee_Unauthenticated_Returns401()
    {
        // Arrange
        var command = new
        {
            EmployeeCode = $"UNAUTH-{Guid.NewGuid():N}".Substring(0, 10),
            FirstName = "Unauth",
            LastName = "Employee",
            IsActive = true
        };

        // Act
        var response = await UnauthenticatedClient.PostAsJsonAsync("/api/employees", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Update Employee Tests

    [Fact]
    public async Task UpdateEmployee_ValidData_ReturnsOk()
    {
        // Arrange - First create an employee
        var createCommand = new
        {
            EmployeeCode = $"UPD-{Guid.NewGuid():N}".Substring(0, 10),
            FirstName = "Original",
            LastName = "Name",
            Email = $"original-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/employees", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<EmployeeDto>>();
        var employeeId = createResult!.Data!.Id;

        // Update the employee
        var updateCommand = new
        {
            EmployeeCode = createCommand.EmployeeCode,
            FirstName = "Updated",
            LastName = "Name",
            Email = createCommand.Email,
            Phone = "0987654321",
            JobTitle = "Updated Position",
            IsActive = true
        };

        // Act
        var response = await AdminClient.PutAsJsonAsync($"/api/employees/{employeeId}", updateCommand);
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<EmployeeDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.FirstName.Should().Be("Updated");
        result.Data.Phone.Should().Be("0987654321");
        result.Data.JobTitle.Should().Be("Updated Position");
    }

    [Fact]
    public async Task UpdateEmployee_NonExisting_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var updateCommand = new
        {
            EmployeeCode = "NONEXIST",
            FirstName = "NonExistent",
            LastName = "Employee",
            IsActive = true
        };

        // Act
        var response = await AdminClient.PutAsJsonAsync($"/api/employees/{nonExistentId}", updateCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateEmployee_WithoutPermission_Returns403()
    {
        // Arrange - First create an employee as Admin
        var createCommand = new
        {
            EmployeeCode = $"PERM-{Guid.NewGuid():N}".Substring(0, 10),
            FirstName = "Permission",
            LastName = "Test",
            Email = $"permission-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/employees", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<EmployeeDto>>();
        var employeeId = createResult!.Data!.Id;

        // Try to update as Operator (no ManageEmployees permission)
        var updateCommand = new
        {
            EmployeeCode = createCommand.EmployeeCode,
            FirstName = "Modified",
            LastName = "ByOperator",
            IsActive = true
        };

        // Act
        var response = await OperatorClient.PutAsJsonAsync($"/api/employees/{employeeId}", updateCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Delete Employee Tests

    [Fact]
    public async Task DeleteEmployee_ExistingEmployee_ReturnsNoContent()
    {
        // Arrange - Create employee to delete
        var createCommand = new
        {
            EmployeeCode = $"DEL-{Guid.NewGuid():N}".Substring(0, 10),
            FirstName = "To",
            LastName = "Delete",
            Email = $"to-delete-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/employees", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<EmployeeDto>>();
        var employeeId = createResult!.Data!.Id;

        // Act
        var response = await AdminClient.DeleteAsync($"/api/employees/{employeeId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deleted (soft delete) - should return NotFound
        var getResponse = await AdminClient.GetAsync($"/api/employees/{employeeId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteEmployee_NonExisting_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await AdminClient.DeleteAsync($"/api/employees/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteEmployee_WithoutPermission_Returns403()
    {
        // Arrange - Create employee as Admin
        var createCommand = new
        {
            EmployeeCode = $"DPERM-{Guid.NewGuid():N}".Substring(0, 10),
            FirstName = "Delete",
            LastName = "Permission",
            Email = $"delete-perm-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/employees", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<EmployeeDto>>();
        var employeeId = createResult!.Data!.Id;

        // Act - Try to delete as Operator
        var response = await OperatorClient.DeleteAsync($"/api/employees/{employeeId}");

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

    #endregion
}
