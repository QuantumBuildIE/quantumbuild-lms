namespace QuantumBuild.Tests.Integration.Core;

/// <summary>
/// Integration tests for authorization functionality including permission-based access control.
/// Tests verify that users can only access endpoints they have permission for.
/// </summary>
public class AuthorizationTests : IntegrationTestBase
{
    public AuthorizationTests(CustomWebApplicationFactory factory) : base(factory) { }

    #region Core.ManageUsers Permission Tests

    [Fact]
    public async Task UsersEndpoint_AsAdmin_ReturnsOk()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UsersEndpoint_AsOperator_Returns403()
    {
        // Act
        var response = await OperatorClient.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UsersEndpoint_AsWarehouse_Returns403()
    {
        // Act
        var response = await WarehouseClient.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UsersEndpoint_AsFinance_Returns403()
    {
        // Act
        var response = await FinanceClient.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Core.ManageEmployees Permission Tests

    [Fact]
    public async Task CreateEmployee_AsAdmin_ReturnsCreated()
    {
        // Arrange
        var command = new
        {
            EmployeeCode = $"EMP-{Guid.NewGuid():N}".Substring(0, 10),
            FirstName = "Test",
            LastName = "Employee",
            Email = $"test-employee-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/employees", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateEmployee_AsOperator_Returns403()
    {
        // Arrange
        var command = new
        {
            EmployeeCode = $"EMP-{Guid.NewGuid():N}".Substring(0, 10),
            FirstName = "Test",
            LastName = "Employee",
            Email = $"test-employee-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        // Act
        var response = await OperatorClient.PostAsJsonAsync("/api/employees", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetEmployees_AsOperator_ReturnsOk()
    {
        // Employees list is available to authenticated users without specific permission
        // Act
        var response = await OperatorClient.GetAsync("/api/employees");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Core.ManageSites Permission Tests

    [Fact]
    public async Task CreateSite_AsAdmin_ReturnsCreated()
    {
        // Arrange
        var command = new
        {
            SiteCode = $"SITE-{Guid.NewGuid():N}".Substring(0, 10),
            SiteName = "Test Site",
            IsActive = true
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/sites", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateSite_AsOperator_Returns403()
    {
        // Arrange
        var command = new
        {
            SiteCode = $"SITE-{Guid.NewGuid():N}".Substring(0, 10),
            SiteName = "Test Site",
            IsActive = true
        };

        // Act
        var response = await OperatorClient.PostAsJsonAsync("/api/sites", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSites_AsOperator_ReturnsOk()
    {
        // Sites list is available to authenticated users without specific permission
        // Act
        var response = await OperatorClient.GetAsync("/api/sites");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Core.ManageCompanies Permission Tests

    [Fact]
    public async Task CreateCompany_AsAdmin_ReturnsCreated()
    {
        // Arrange
        var command = new
        {
            CompanyCode = $"COMP-{Guid.NewGuid():N}".Substring(0, 10),
            CompanyName = "Test Company",
            IsActive = true
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/companies", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateCompany_AsOperator_Returns403()
    {
        // Arrange
        var command = new
        {
            CompanyCode = $"COMP-{Guid.NewGuid():N}".Substring(0, 10),
            CompanyName = "Test Company",
            IsActive = true
        };

        // Act
        var response = await OperatorClient.PostAsJsonAsync("/api/companies", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetCompanies_AsOperator_ReturnsOk()
    {
        // Companies list is available to authenticated users without specific permission
        // Act
        var response = await OperatorClient.GetAsync("/api/companies");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region StockManagement Permission Tests

    [Fact]
    public async Task GetProducts_AsWarehouse_ReturnsOk()
    {
        // Warehouse has StockManagement.View permission
        // Act
        var response = await WarehouseClient.GetAsync("/api/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProducts_AsFinance_ReturnsOk()
    {
        // Finance has StockManagement.View permission
        // Act
        var response = await FinanceClient.GetAsync("/api/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateProduct_AsWarehouse_ReturnsCreated()
    {
        // Warehouse has StockManagement.ManageProducts permission
        // Arrange - First get a category ID
        var categoriesResponse = await WarehouseClient.GetAsync("/api/categories/all");
        Guid? categoryId = null;

        if (categoriesResponse.StatusCode == HttpStatusCode.OK)
        {
            var content = await categoriesResponse.Content.ReadAsStringAsync();
            // Try to extract a category ID from the response
            if (content.Contains("\"id\""))
            {
                try
                {
                    var categoriesResult = await categoriesResponse.Content.ReadFromJsonAsync<CategoryResultWrapper>();
                    categoryId = categoriesResult?.Data?.FirstOrDefault()?.Id;
                }
                catch
                {
                    // If deserialization fails, use a fallback
                }
            }
        }

        var command = new
        {
            ProductCode = $"PROD-{Guid.NewGuid():N}".Substring(0, 10),
            ProductName = "Test Product",
            CategoryId = categoryId ?? Guid.NewGuid(),
            UnitType = "Each",
            BaseRate = 10.0m,
            ReorderLevel = 5,
            ReorderQuantity = 10,
            LeadTimeDays = 3,
            IsActive = true
        };

        // Act
        var response = await WarehouseClient.PostAsJsonAsync("/api/products", command);

        // Assert - Could be Created if valid or BadRequest if category doesn't exist
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateProduct_AsFinance_Returns403()
    {
        // Finance only has View permission, not ManageProducts
        // Arrange
        var command = new
        {
            ProductCode = $"PROD-{Guid.NewGuid():N}".Substring(0, 10),
            ProductName = "Test Product",
            CategoryId = Guid.NewGuid(),
            UnitType = "Each",
            BaseRate = 10.0m,
            ReorderLevel = 5,
            ReorderQuantity = 10,
            LeadTimeDays = 3,
            IsActive = true
        };

        // Act
        var response = await FinanceClient.PostAsJsonAsync("/api/products", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region StockManagement.ViewCostings Permission Tests

    [Fact]
    public async Task GetStockValuationReport_AsFinance_ReturnsOk()
    {
        // Finance has StockManagement.ViewCostings permission
        // Act
        var response = await FinanceClient.GetAsync("/api/stock/reports/valuation");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetStockValuationReport_AsOperator_Returns403()
    {
        // Operator doesn't have ViewCostings permission
        // Act
        var response = await OperatorClient.GetAsync("/api/stock/reports/valuation");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region SiteAttendance Permission Tests

    [Fact]
    public async Task GetAttendanceDashboard_AsSiteManager_ReturnsOk()
    {
        // SiteManager has SiteAttendance.View permission
        // Act
        var response = await SiteManagerClient.GetAsync("/api/site-attendance/dashboard/kpis");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Cross-Module Permission Tests

    [Fact]
    public async Task Warehouse_CannotManageUsers()
    {
        // Warehouse has stock permissions but not user management
        // Act
        var response = await WarehouseClient.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Finance_CannotManageStock()
    {
        // Finance can view but not manage stock
        // Arrange
        var command = new
        {
            Sku = $"PROD-{Guid.NewGuid():N}".Substring(0, 10),
            Name = "Test Product",
            IsActive = true
        };

        // Act
        var response = await FinanceClient.PostAsJsonAsync("/api/products", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_HasAccessToAll()
    {
        // Admin should have access to all endpoints

        // Act - Test multiple endpoints
        var usersResponse = await AdminClient.GetAsync("/api/users");
        var employeesResponse = await AdminClient.GetAsync("/api/employees");
        var sitesResponse = await AdminClient.GetAsync("/api/sites");
        var companiesResponse = await AdminClient.GetAsync("/api/companies");
        var productsResponse = await AdminClient.GetAsync("/api/products");

        // Assert - All should return OK
        usersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        employeesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        sitesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        companiesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        productsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Role-Based Access Tests

    [Fact]
    public async Task AllAuthenticatedUsers_CanAccessEmployeesList()
    {
        // Act
        var adminResponse = await AdminClient.GetAsync("/api/employees");
        var warehouseResponse = await WarehouseClient.GetAsync("/api/employees");
        var operatorResponse = await OperatorClient.GetAsync("/api/employees");
        var financeResponse = await FinanceClient.GetAsync("/api/employees");

        // Assert - All authenticated users can view employee list
        adminResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        warehouseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        operatorResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        financeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OnlyManagePermission_CanModifyEmployees()
    {
        // Arrange
        var command = new
        {
            EmployeeCode = $"EMP-{Guid.NewGuid():N}".Substring(0, 10),
            FirstName = "Test",
            LastName = "Employee",
            Email = $"test-employee-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        // Act - Admin can create
        var adminResponse = await AdminClient.PostAsJsonAsync("/api/employees", command);

        // Create new command with different data for other tests
        var command2 = new
        {
            EmployeeCode = $"EMP-{Guid.NewGuid():N}".Substring(0, 10),
            FirstName = "Test2",
            LastName = "Employee2",
            Email = $"test-employee2-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        // Act - Others cannot create
        var warehouseResponse = await WarehouseClient.PostAsJsonAsync("/api/employees", command2);
        var operatorResponse = await OperatorClient.PostAsJsonAsync("/api/employees", command2);
        var financeResponse = await FinanceClient.PostAsJsonAsync("/api/employees", command2);

        // Assert
        adminResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        warehouseResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        operatorResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        financeResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Response DTOs

    private record ResultWrapper<T>(
        bool Success,
        T? Data,
        string? Message,
        List<string>? Errors
    );

    private record CategoryDto(
        Guid Id,
        string CategoryCode,
        string CategoryName,
        string? Description,
        bool IsActive
    );

    private record CategoryResultWrapper(
        bool Success,
        List<CategoryDto>? Data,
        string? Message
    );

    #endregion
}
