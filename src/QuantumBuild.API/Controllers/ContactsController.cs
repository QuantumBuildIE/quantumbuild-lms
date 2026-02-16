using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Features.Contacts;
using QuantumBuild.Core.Application.Features.Contacts.DTOs;

namespace QuantumBuild.API.Controllers;

[ApiController]
[Route("api/companies/{companyId:guid}/contacts")]
[Authorize]
public class ContactsController : ControllerBase
{
    private readonly IContactService _contactService;

    public ContactsController(IContactService contactService)
    {
        _contactService = contactService;
    }

    /// <summary>
    /// Get all contacts for a company
    /// </summary>
    /// <param name="companyId">Company ID</param>
    /// <returns>List of contacts</returns>
    [HttpGet]
    public async Task<IActionResult> GetByCompany(Guid companyId)
    {
        var result = await _contactService.GetByCompanyIdAsync(companyId);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get a contact by ID
    /// </summary>
    /// <param name="companyId">Company ID</param>
    /// <param name="id">Contact ID</param>
    /// <returns>Contact details</returns>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid companyId, Guid id)
    {
        var result = await _contactService.GetByIdAsync(id);

        if (!result.Success)
        {
            return NotFound(result);
        }

        // Verify the contact belongs to the specified company
        if (result.Data!.CompanyId != companyId)
        {
            return NotFound(new { success = false, message = "Contact not found for this company" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Create a new contact for a company
    /// </summary>
    /// <param name="companyId">Company ID</param>
    /// <param name="dto">Contact creation data</param>
    /// <returns>Created contact</returns>
    [HttpPost]
    [Authorize(Policy = "Core.ManageCompanies")]
    public async Task<IActionResult> Create(Guid companyId, [FromBody] CreateContactDto dto)
    {
        // Ensure the contact is created for the specified company
        var createDto = dto with { CompanyId = companyId };
        var result = await _contactService.CreateAsync(createDto);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return CreatedAtAction(nameof(GetById), new { companyId, id = result.Data!.Id }, result);
    }

    /// <summary>
    /// Update an existing contact
    /// </summary>
    /// <param name="companyId">Company ID</param>
    /// <param name="id">Contact ID</param>
    /// <param name="dto">Contact update data</param>
    /// <returns>Updated contact</returns>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Core.ManageCompanies")]
    public async Task<IActionResult> Update(Guid companyId, Guid id, [FromBody] UpdateContactDto dto)
    {
        // Verify the contact exists and belongs to the company
        var existingResult = await _contactService.GetByIdAsync(id);
        if (!existingResult.Success || existingResult.Data!.CompanyId != companyId)
        {
            return NotFound(new { success = false, message = "Contact not found for this company" });
        }

        // Ensure the contact stays with the specified company
        var updateDto = dto with { CompanyId = companyId };
        var result = await _contactService.UpdateAsync(id, updateDto);

        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Delete a contact (soft delete)
    /// </summary>
    /// <param name="companyId">Company ID</param>
    /// <param name="id">Contact ID</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Core.ManageCompanies")]
    public async Task<IActionResult> Delete(Guid companyId, Guid id)
    {
        // Verify the contact exists and belongs to the company
        var existingResult = await _contactService.GetByIdAsync(id);
        if (!existingResult.Success || existingResult.Data!.CompanyId != companyId)
        {
            return NotFound(new { success = false, message = "Contact not found for this company" });
        }

        var result = await _contactService.DeleteAsync(id);

        if (!result.Success)
        {
            return NotFound(result);
        }

        return NoContent();
    }
}

/// <summary>
/// Additional controller for managing contacts independently (not nested under company)
/// </summary>
[ApiController]
[Route("api/contacts")]
[Authorize]
public class AllContactsController : ControllerBase
{
    private readonly IContactService _contactService;

    public AllContactsController(IContactService contactService)
    {
        _contactService = contactService;
    }

    /// <summary>
    /// Get all contacts (non-paginated)
    /// </summary>
    /// <returns>List of all contacts</returns>
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var result = await _contactService.GetAllAsync();

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get contacts with pagination, sorting, and search
    /// </summary>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 20)</param>
    /// <param name="sortColumn">Column to sort by</param>
    /// <param name="sortDirection">Sort direction (asc/desc)</param>
    /// <param name="search">Search term</param>
    /// <param name="companyId">Filter by company ID</param>
    /// <returns>Paginated list of contacts</returns>
    [HttpGet]
    public async Task<IActionResult> GetPaginated(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortColumn = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] string? search = null,
        [FromQuery] Guid? companyId = null)
    {
        var query = new GetContactsQueryDto(pageNumber, pageSize, sortColumn, sortDirection, search, companyId);
        var result = await _contactService.GetPaginatedAsync(query);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get a contact by ID
    /// </summary>
    /// <param name="id">Contact ID</param>
    /// <returns>Contact details</returns>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _contactService.GetByIdAsync(id);

        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Create a new contact
    /// </summary>
    /// <param name="dto">Contact creation data</param>
    /// <returns>Created contact</returns>
    [HttpPost]
    [Authorize(Policy = "Core.ManageCompanies")]
    public async Task<IActionResult> Create([FromBody] CreateContactDto dto)
    {
        var result = await _contactService.CreateAsync(dto);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
    }

    /// <summary>
    /// Update an existing contact
    /// </summary>
    /// <param name="id">Contact ID</param>
    /// <param name="dto">Contact update data</param>
    /// <returns>Updated contact</returns>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Core.ManageCompanies")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContactDto dto)
    {
        var result = await _contactService.UpdateAsync(id, dto);

        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Delete a contact (soft delete)
    /// </summary>
    /// <param name="id">Contact ID</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Core.ManageCompanies")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _contactService.DeleteAsync(id);

        if (!result.Success)
        {
            return NotFound(result);
        }

        return NoContent();
    }
}
