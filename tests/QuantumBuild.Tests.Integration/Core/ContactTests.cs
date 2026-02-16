namespace QuantumBuild.Tests.Integration.Core;

/// <summary>
/// Integration tests for Contact CRUD operations.
/// Contacts can be accessed both through nested routes (/api/companies/{companyId}/contacts)
/// and through the independent route (/api/contacts).
/// </summary>
public class ContactTests : IntegrationTestBase
{
    public ContactTests(CustomWebApplicationFactory factory) : base(factory) { }

    #region Helper Methods

    private async Task<Guid> CreateTestCompanyAsync()
    {
        var command = new
        {
            CompanyCode = $"CTCO-{Guid.NewGuid():N}".Substring(0, 10),
            CompanyName = $"Contact Test Company {Guid.NewGuid():N}".Substring(0, 30),
            IsActive = true
        };

        var response = await AdminClient.PostAsJsonAsync("/api/companies", command);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<CompanyDto>>();
        return result!.Data!.Id;
    }

    #endregion

    #region Get Contacts Tests (Independent Route)

    [Fact]
    public async Task GetContacts_ReturnsPagedResults()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/contacts?pageNumber=1&pageSize=10");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<ContactDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.PageNumber.Should().Be(1);
        result.Data.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetContacts_FilterByCompanyId_ReturnsFilteredResults()
    {
        // Arrange - Create a company and contact
        var companyId = await CreateTestCompanyAsync();

        var createCommand = new
        {
            FirstName = "Company",
            LastName = "Contact",
            CompanyId = companyId,
            Email = $"company-contact-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/contacts", createCommand);
        createResponse.EnsureSuccessStatusCode();

        // Act - Filter by company ID
        var response = await AdminClient.GetAsync($"/api/contacts?companyId={companyId}");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<PaginatedResult<ContactDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Items.Should().OnlyContain(c => c.CompanyId == companyId);
    }

    [Fact]
    public async Task GetAllContacts_ReturnsNonPaginatedList()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/contacts/all");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<List<ContactDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetContactById_ExistingContact_ReturnsContact()
    {
        // Arrange - Create a contact
        var companyId = await CreateTestCompanyAsync();
        var createCommand = new
        {
            FirstName = "GetById",
            LastName = "Contact",
            CompanyId = companyId,
            Email = $"getbyid-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/contacts", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<ContactDto>>();
        var contactId = createResult!.Data!.Id;

        // Act
        var response = await AdminClient.GetAsync($"/api/contacts/{contactId}");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<ContactDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Id.Should().Be(contactId);
        result.Data.FirstName.Should().Be("GetById");
    }

    [Fact]
    public async Task GetContactById_NonExistingContact_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await AdminClient.GetAsync($"/api/contacts/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Get Contacts by Company (Nested Route)

    [Fact]
    public async Task GetContactsByCompany_ReturnsCompanyContacts()
    {
        // Arrange - Create a company and contacts
        var companyId = await CreateTestCompanyAsync();

        // Create multiple contacts for the company
        for (int i = 1; i <= 3; i++)
        {
            var command = new
            {
                FirstName = $"Contact{i}",
                LastName = "Test",
                CompanyId = companyId,
                Email = $"contact{i}-{Guid.NewGuid():N}@test.quantumbuild.ie",
                IsActive = true
            };
            var resp = await AdminClient.PostAsJsonAsync($"/api/companies/{companyId}/contacts", command);
            resp.EnsureSuccessStatusCode();
        }

        // Act
        var response = await AdminClient.GetAsync($"/api/companies/{companyId}/contacts");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<List<ContactDto>>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task GetContactByIdNested_ExistingContact_ReturnsContact()
    {
        // Arrange - Create a company and contact
        var companyId = await CreateTestCompanyAsync();
        var createCommand = new
        {
            FirstName = "Nested",
            LastName = "Contact",
            CompanyId = companyId,
            Email = $"nested-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync($"/api/companies/{companyId}/contacts", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<ContactDto>>();
        var contactId = createResult!.Data!.Id;

        // Act
        var response = await AdminClient.GetAsync($"/api/companies/{companyId}/contacts/{contactId}");
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<ContactDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Data!.FirstName.Should().Be("Nested");
        result.Data.CompanyId.Should().Be(companyId);
    }

    [Fact]
    public async Task GetContactByIdNested_WrongCompany_ReturnsNotFound()
    {
        // Arrange - Create two companies
        var companyId1 = await CreateTestCompanyAsync();
        var companyId2 = await CreateTestCompanyAsync();

        // Create contact for company 1
        var createCommand = new
        {
            FirstName = "Wrong",
            LastName = "Company",
            CompanyId = companyId1,
            Email = $"wrong-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync($"/api/companies/{companyId1}/contacts", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<ContactDto>>();
        var contactId = createResult!.Data!.Id;

        // Act - Try to get the contact using company 2's route
        var response = await AdminClient.GetAsync($"/api/companies/{companyId2}/contacts/{contactId}");

        // Assert - Should fail because contact doesn't belong to company 2
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Create Contact Tests

    [Fact]
    public async Task CreateContact_ValidData_ReturnsCreated()
    {
        // Arrange
        var companyId = await CreateTestCompanyAsync();
        var command = new
        {
            FirstName = "New",
            LastName = "Contact",
            JobTitle = "Manager",
            Email = $"new-contact-{Guid.NewGuid():N}@test.quantumbuild.ie",
            Phone = "01234567890",
            Mobile = "0871234567",
            CompanyId = companyId,
            IsPrimaryContact = true,
            IsActive = true,
            Notes = "Test contact created by integration test"
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/contacts", command);
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<ContactDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Id.Should().NotBeEmpty();
        result.Data.FirstName.Should().Be("New");
        result.Data.LastName.Should().Be("Contact");
        result.Data.JobTitle.Should().Be("Manager");
        result.Data.IsPrimaryContact.Should().BeTrue();
    }

    [Fact]
    public async Task CreateContactNested_ValidData_ReturnsCreated()
    {
        // Arrange
        var companyId = await CreateTestCompanyAsync();
        var command = new
        {
            FirstName = "Nested",
            LastName = "NewContact",
            Email = $"nested-new-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        // Act - Use nested route
        var response = await AdminClient.PostAsJsonAsync($"/api/companies/{companyId}/contacts", command);
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<ContactDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.CompanyId.Should().Be(companyId); // Should be assigned to the company
    }

    [Fact]
    public async Task CreateContact_MissingRequiredFields_ReturnsBadRequest()
    {
        // Arrange - Missing FirstName and LastName
        var command = new
        {
            Email = $"incomplete-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/contacts", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateContact_Unauthenticated_Returns401()
    {
        // Arrange
        var command = new
        {
            FirstName = "Unauth",
            LastName = "Contact",
            IsActive = true
        };

        // Act
        var response = await UnauthenticatedClient.PostAsJsonAsync("/api/contacts", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateContact_WithoutPermission_Returns403()
    {
        // Arrange
        var command = new
        {
            FirstName = "NoPerm",
            LastName = "Contact",
            IsActive = true
        };

        // Act - Operator doesn't have ManageCompanies permission (which is required for contacts)
        var response = await OperatorClient.PostAsJsonAsync("/api/contacts", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateContact_WithSite_ReturnsCreatedWithSite()
    {
        // Arrange - Get existing site
        var sitesResponse = await AdminClient.GetAsync("/api/sites/all");
        var sitesResult = await sitesResponse.Content.ReadFromJsonAsync<ResultWrapper<List<SiteDto>>>();
        var site = sitesResult!.Data!.FirstOrDefault();

        var companyId = await CreateTestCompanyAsync();
        var command = new
        {
            FirstName = "Site",
            LastName = "Contact",
            CompanyId = companyId,
            SiteId = site?.Id,
            Email = $"site-contact-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        // Act
        var response = await AdminClient.PostAsJsonAsync("/api/contacts", command);
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<ContactDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        if (site != null)
        {
            result!.Data!.SiteId.Should().Be(site.Id);
        }
    }

    #endregion

    #region Update Contact Tests

    [Fact]
    public async Task UpdateContact_ValidData_ReturnsOk()
    {
        // Arrange - Create a contact
        var companyId = await CreateTestCompanyAsync();
        var createCommand = new
        {
            FirstName = "Original",
            LastName = "Contact",
            CompanyId = companyId,
            Email = $"original-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/contacts", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<ContactDto>>();
        var contactId = createResult!.Data!.Id;

        // Update the contact
        var updateCommand = new
        {
            FirstName = "Updated",
            LastName = "ContactName",
            CompanyId = companyId,
            JobTitle = "Updated Position",
            Phone = "0987654321",
            IsActive = true
        };

        // Act
        var response = await AdminClient.PutAsJsonAsync($"/api/contacts/{contactId}", updateCommand);
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<ContactDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.FirstName.Should().Be("Updated");
        result.Data.LastName.Should().Be("ContactName");
        result.Data.JobTitle.Should().Be("Updated Position");
    }

    [Fact]
    public async Task UpdateContactNested_ValidData_ReturnsOk()
    {
        // Arrange - Create a contact using nested route
        var companyId = await CreateTestCompanyAsync();
        var createCommand = new
        {
            FirstName = "Nested",
            LastName = "Update",
            CompanyId = companyId,
            Email = $"nested-update-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync($"/api/companies/{companyId}/contacts", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<ContactDto>>();
        var contactId = createResult!.Data!.Id;

        // Update using nested route
        var updateCommand = new
        {
            FirstName = "NestedUpdated",
            LastName = "Contact",
            CompanyId = companyId,
            IsActive = true
        };

        // Act
        var response = await AdminClient.PutAsJsonAsync($"/api/companies/{companyId}/contacts/{contactId}", updateCommand);
        var result = await response.Content.ReadFromJsonAsync<ResultWrapper<ContactDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Data!.FirstName.Should().Be("NestedUpdated");
    }

    [Fact]
    public async Task UpdateContact_NonExisting_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var updateCommand = new
        {
            FirstName = "NonExistent",
            LastName = "Contact",
            IsActive = true
        };

        // Act
        var response = await AdminClient.PutAsJsonAsync($"/api/contacts/{nonExistentId}", updateCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateContact_WithoutPermission_Returns403()
    {
        // Arrange - Create a contact as Admin
        var companyId = await CreateTestCompanyAsync();
        var createCommand = new
        {
            FirstName = "Permission",
            LastName = "Test",
            CompanyId = companyId,
            Email = $"permission-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/contacts", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<ContactDto>>();
        var contactId = createResult!.Data!.Id;

        // Try to update as Operator
        var updateCommand = new
        {
            FirstName = "Modified",
            LastName = "ByOperator",
            IsActive = true
        };

        // Act
        var response = await OperatorClient.PutAsJsonAsync($"/api/contacts/{contactId}", updateCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Delete Contact Tests

    [Fact]
    public async Task DeleteContact_ExistingContact_ReturnsNoContent()
    {
        // Arrange - Create contact to delete
        var companyId = await CreateTestCompanyAsync();
        var createCommand = new
        {
            FirstName = "To",
            LastName = "Delete",
            CompanyId = companyId,
            Email = $"to-delete-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/contacts", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<ContactDto>>();
        var contactId = createResult!.Data!.Id;

        // Act
        var response = await AdminClient.DeleteAsync($"/api/contacts/{contactId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deleted (soft delete) - should return NotFound
        var getResponse = await AdminClient.GetAsync($"/api/contacts/{contactId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteContactNested_ExistingContact_ReturnsNoContent()
    {
        // Arrange - Create contact using nested route
        var companyId = await CreateTestCompanyAsync();
        var createCommand = new
        {
            FirstName = "Nested",
            LastName = "Delete",
            CompanyId = companyId,
            Email = $"nested-delete-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync($"/api/companies/{companyId}/contacts", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<ContactDto>>();
        var contactId = createResult!.Data!.Id;

        // Act - Delete using nested route
        var response = await AdminClient.DeleteAsync($"/api/companies/{companyId}/contacts/{contactId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteContact_NonExisting_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await AdminClient.DeleteAsync($"/api/contacts/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteContact_WithoutPermission_Returns403()
    {
        // Arrange - Create contact as Admin
        var companyId = await CreateTestCompanyAsync();
        var createCommand = new
        {
            FirstName = "Delete",
            LastName = "Permission",
            CompanyId = companyId,
            Email = $"delete-perm-{Guid.NewGuid():N}@test.quantumbuild.ie",
            IsActive = true
        };

        var createResponse = await AdminClient.PostAsJsonAsync("/api/contacts", createCommand);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<ResultWrapper<ContactDto>>();
        var contactId = createResult!.Data!.Id;

        // Act - Try to delete as Operator
        var response = await OperatorClient.DeleteAsync($"/api/contacts/{contactId}");

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

    private record ContactDto(
        Guid Id,
        string FirstName,
        string LastName,
        string FullName,
        string? JobTitle,
        string? Email,
        string? Phone,
        string? Mobile,
        Guid? CompanyId,
        string? CompanyName,
        Guid? SiteId,
        string? SiteName,
        bool IsPrimaryContact,
        bool IsActive,
        string? Notes
    );

    private record CompanyDto(
        Guid Id,
        string CompanyCode,
        string CompanyName
    );

    private record SiteDto(
        Guid Id,
        string SiteCode,
        string SiteName
    );

    #endregion
}
