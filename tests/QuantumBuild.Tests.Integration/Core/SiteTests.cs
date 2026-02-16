namespace QuantumBuild.Tests.Integration.Core;

/// <summary>
/// Integration tests for Site CRUD operations.
/// </summary>
public class SiteTests : IntegrationTestBase
{
    public SiteTests(CustomWebApplicationFactory factory) : base(factory) { }

    #region Get Sites Tests

    [Fact]
    public async Task GetSites_ReturnsPagedResults()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/sites?pageNumber=1&pageSize=10");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<SiteDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.PageNumber.Should().Be(1);
        result.Data.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetSites_WithSearch_ReturnsFilteredResults()
    {
        // Arrange - First create a site with a unique name
        var createCommand = new
        {
            SiteCode = $"SRCH-{Guid.NewGuid():N}".Substring(0, 10),
            SiteName = "UniqueSearchSiteName",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/sites", createCommand);
        createResponse.EnsureSuccessStatusCode();

        // Act - Search for the unique name
        var response = await AdminClient.GetAsync("/api/sites?search=UniqueSearchSiteName");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<SiteDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Items.Should().Contain(s => s.SiteName == "UniqueSearchSiteName");
    }

    [Fact]
    public async Task GetAllSites_ReturnsNonPaginatedList()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/sites/all");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<List<SiteDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSiteById_ExistingSite_ReturnsSite()
    {
        // Arrange - First create a site
        var createCommand = new
        {
            SiteCode = $"GET-{Guid.NewGuid():N}".Substring(0, 10),
            SiteName = "GetById Site",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/sites", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<SiteDto>>();
        var siteId = createResult!.Data!.Id;

        // Act
        var response = await AdminClient.GetAsync($"/api/sites/{siteId}");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<SiteDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Id.Should().Be(siteId);
        result.Data.SiteName.Should().Be("GetById Site");
    }

    [Fact]
    public async Task GetSiteById_NonExistingSite_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await AdminClient.GetAsync($"/api/sites/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Create Site Tests

    [Fact]
    public async Task CreateSite_ValidData_ReturnsCreated()
    {
        // Arrange
        var command = new
        {
            SiteCode = $"NEW-{Guid.NewGuid():N}".Substring(0, 10),
            SiteName = "New Test Site",
            Address = "123 Test Street",
            City = "Dublin",
            PostalCode = "D01 ABC1",
            Phone = "01234567890",
            Email = $"site-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true,
            Notes = "Test site created by integration test"
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/sites", command);
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<SiteDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Id.Should().NotBeEmpty();
        result.Data.SiteName.Should().Be("New Test Site");
        result.Data.City.Should().Be("Dublin");
    }

    [Fact]
    public async Task CreateSite_WithGeolocation_ReturnsCreatedWithCoordinates()
    {
        // Arrange
        var command = new
        {
            SiteCode = $"GEO-{Guid.NewGuid():N}".Substring(0, 10),
            SiteName = "Geolocation Site",
            Latitude = 53.3498m,
            Longitude = -6.2603m,
            GeofenceRadiusMeters = 150,
            IsActive = true
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/sites", command);
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<SiteDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Latitude.Should().Be(53.3498m);
        result.Data.Longitude.Should().Be(-6.2603m);
        result.Data.GeofenceRadiusMeters.Should().Be(150);
    }

    [Fact]
    public async Task CreateSite_WithCompany_ReturnsCreatedWithCompany()
    {
        // Arrange - First get an existing company
        var companiesResponse = await AdminClient.GetAsync("/api/companies/all");
        var companiesResult = await companiesResponse.Content.ReadFromJsonAsync<ResultWrapper<List<CompanyDto>>>();
        var company = companiesResult!.Data!.FirstOrDefault();

        var command = new
        {
            SiteCode = $"COMP-{Guid.NewGuid():N}".Substring(0, 10),
            SiteName = "Company Site",
            CompanyId = company?.Id,
            IsActive = true
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/sites", command);
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<SiteDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        if (company != null)
        {
            result.Data!.CompanyId.Should().Be(company.Id);
        }
    }

    [Fact]
    public async Task CreateSite_MissingRequiredFields_ReturnsBadRequest()
    {
        // Arrange - Missing SiteCode and SiteName
        var command = new
        {
            City = "Dublin",
            IsActive = true
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/sites", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSite_DuplicateSiteCode_ReturnsBadRequest()
    {
        // Arrange - First create a site
        var siteCode = $"DUP-{Guid.NewGuid():N}".Substring(0, 10);
        var command1 = new
        {
            SiteCode = siteCode,
            SiteName = "First Site",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/sites", command1);
        createResponse.EnsureSuccessStatusCode();

        // Try to create another site with the same code
        var command2 = new
        {
            SiteCode = siteCode, // Same code
            SiteName = "Second Site",
            IsActive = true
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/sites", command2);

        // Assert - Should fail due to duplicate code
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSite_Unauthenticated_Returns401()
    {
        // Arrange
        var command = new
        {
            SiteCode = $"UNAUTH-{Guid.NewGuid():N}".Substring(0, 10),
            SiteName = "Unauth Site",
            IsActive = true
        };

        // Act
        var response = await UnauthenticatedClient.PostAsJsonAsync("/api/sites", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateSite_WithoutPermission_Returns403()
    {
        // Arrange
        var command = new
        {
            SiteCode = $"NOPERM-{Guid.NewGuid():N}".Substring(0, 10),
            SiteName = "No Permission Site",
            IsActive = true
        };

        // Act - Operator doesn't have ManageSites permission
        var response = await OperatorClient.PostAsJsonAsync("/api/sites", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Update Site Tests

    [Fact]
    public async Task UpdateSite_ValidData_ReturnsOk()
    {
        // Arrange - First create a site
        var createCommand = new
        {
            SiteCode = $"UPD-{Guid.NewGuid():N}".Substring(0, 10),
            SiteName = "Original Site",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/sites", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<SiteDto>>();
        var siteId = createResult!.Data!.Id;

        // Update the site
        var updateCommand = new
        {
            SiteCode = createCommand.SiteCode,
            SiteName = "Updated Site Name",
            City = "Cork",
            Phone = "0214567890",
            IsActive = true
        };

        // Act
        var response = await AdminClient.PutAsJsonAsync($"/api/sites/{siteId}", updateCommand);
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<SiteDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.SiteName.Should().Be("Updated Site Name");
        result.Data.City.Should().Be("Cork");
        result.Data.Phone.Should().Be("0214567890");
    }

    [Fact]
    public async Task UpdateSite_UpdateGeolocation_ReturnsOk()
    {
        // Arrange - First create a site
        var createCommand = new
        {
            SiteCode = $"UPDGEO-{Guid.NewGuid():N}".Substring(0, 10),
            SiteName = "Geo Update Site",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/sites", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<SiteDto>>();
        var siteId = createResult!.Data!.Id;

        // Update with geolocation
        var updateCommand = new
        {
            SiteCode = createCommand.SiteCode,
            SiteName = createCommand.SiteName,
            Latitude = 51.8985m,
            Longitude = -8.4756m,
            GeofenceRadiusMeters = 200,
            IsActive = true
        };

        // Act
        var response = await AdminClient.PutAsJsonAsync($"/api/sites/{siteId}", updateCommand);
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<SiteDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Latitude.Should().Be(51.8985m);
        result.Data.Longitude.Should().Be(-8.4756m);
        result.Data.GeofenceRadiusMeters.Should().Be(200);
    }

    [Fact]
    public async Task UpdateSite_NonExisting_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var updateCommand = new
        {
            SiteCode = "NONEXIST",
            SiteName = "NonExistent Site",
            IsActive = true
        };

        // Act
        var response = await AdminClient.PutAsJsonAsync($"/api/sites/{nonExistentId}", updateCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateSite_WithoutPermission_Returns403()
    {
        // Arrange - First create a site as Admin
        var createCommand = new
        {
            SiteCode = $"PERM-{Guid.NewGuid():N}".Substring(0, 10),
            SiteName = "Permission Test Site",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/sites", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<SiteDto>>();
        var siteId = createResult!.Data!.Id;

        // Try to update as Operator
        var updateCommand = new
        {
            SiteCode = createCommand.SiteCode,
            SiteName = "Modified By Operator",
            IsActive = true
        };

        // Act
        var response = await OperatorClient.PutAsJsonAsync($"/api/sites/{siteId}", updateCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Delete Site Tests

    [Fact]
    public async Task DeleteSite_ExistingSite_ReturnsNoContent()
    {
        // Arrange - Create site to delete
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

        // Act
        var response = await AdminClient.DeleteAsync($"/api/sites/{siteId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deleted (soft delete) - should return NotFound
        var getResponse = await AdminClient.GetAsync($"/api/sites/{siteId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteSite_NonExisting_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await AdminClient.DeleteAsync($"/api/sites/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteSite_WithoutPermission_Returns403()
    {
        // Arrange - Create site as Admin
        var createCommand = new
        {
            SiteCode = $"DPERM-{Guid.NewGuid():N}".Substring(0, 10),
            SiteName = "Delete Permission Site",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/sites", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<SiteDto>>();
        var siteId = createResult!.Data!.Id;

        // Act - Try to delete as Operator
        var response = await OperatorClient.DeleteAsync($"/api/sites/{siteId}");

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
        string CompanyName
    );

    #endregion
}
