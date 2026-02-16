using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Features.Contacts.DTOs;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Application.Features.Contacts;

public class ContactService : IContactService
{
    private readonly ICoreDbContext _context;

    public ContactService(ICoreDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<ContactDto>>> GetAllAsync()
    {
        try
        {
            var contacts = await _context.Contacts
                .Include(c => c.Company)
                .Include(c => c.Site)
                .OrderBy(c => c.LastName)
                .ThenBy(c => c.FirstName)
                .Select(c => new ContactDto(
                    c.Id,
                    c.FirstName,
                    c.LastName,
                    c.FirstName + " " + c.LastName,
                    c.JobTitle,
                    c.Email,
                    c.Phone,
                    c.Mobile,
                    c.CompanyId,
                    c.Company != null ? c.Company.CompanyName : null,
                    c.SiteId,
                    c.Site != null ? c.Site.SiteName : null,
                    c.IsPrimaryContact,
                    c.IsActive,
                    c.Notes
                ))
                .ToListAsync();

            return Result.Ok(contacts);
        }
        catch (Exception ex)
        {
            return Result.Fail<List<ContactDto>>($"Error retrieving contacts: {ex.Message}");
        }
    }

    public async Task<Result<List<ContactDto>>> GetByCompanyIdAsync(Guid companyId)
    {
        try
        {
            var contacts = await _context.Contacts
                .Include(c => c.Company)
                .Include(c => c.Site)
                .Where(c => c.CompanyId == companyId)
                .OrderBy(c => c.IsPrimaryContact ? 0 : 1)
                .ThenBy(c => c.LastName)
                .ThenBy(c => c.FirstName)
                .Select(c => new ContactDto(
                    c.Id,
                    c.FirstName,
                    c.LastName,
                    c.FirstName + " " + c.LastName,
                    c.JobTitle,
                    c.Email,
                    c.Phone,
                    c.Mobile,
                    c.CompanyId,
                    c.Company != null ? c.Company.CompanyName : null,
                    c.SiteId,
                    c.Site != null ? c.Site.SiteName : null,
                    c.IsPrimaryContact,
                    c.IsActive,
                    c.Notes
                ))
                .ToListAsync();

            return Result.Ok(contacts);
        }
        catch (Exception ex)
        {
            return Result.Fail<List<ContactDto>>($"Error retrieving contacts: {ex.Message}");
        }
    }

    public async Task<Result<PaginatedList<ContactDto>>> GetPaginatedAsync(GetContactsQueryDto query)
    {
        try
        {
            var contactsQuery = _context.Contacts
                .Include(c => c.Company)
                .Include(c => c.Site)
                .AsQueryable();

            // Apply company filter
            if (query.CompanyId.HasValue)
            {
                contactsQuery = contactsQuery.Where(c => c.CompanyId == query.CompanyId.Value);
            }

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var searchLower = query.Search.ToLower();
                contactsQuery = contactsQuery.Where(c =>
                    c.FirstName.ToLower().Contains(searchLower) ||
                    c.LastName.ToLower().Contains(searchLower) ||
                    (c.FirstName + " " + c.LastName).ToLower().Contains(searchLower) ||
                    (c.Email != null && c.Email.ToLower().Contains(searchLower)) ||
                    (c.JobTitle != null && c.JobTitle.ToLower().Contains(searchLower))
                );
            }

            // Apply sorting
            contactsQuery = ApplySorting(contactsQuery, query.SortColumn, query.SortDirection);

            // Get total count before pagination
            var totalCount = await contactsQuery.CountAsync();

            // Apply pagination
            var contacts = await contactsQuery
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(c => new ContactDto(
                    c.Id,
                    c.FirstName,
                    c.LastName,
                    c.FirstName + " " + c.LastName,
                    c.JobTitle,
                    c.Email,
                    c.Phone,
                    c.Mobile,
                    c.CompanyId,
                    c.Company != null ? c.Company.CompanyName : null,
                    c.SiteId,
                    c.Site != null ? c.Site.SiteName : null,
                    c.IsPrimaryContact,
                    c.IsActive,
                    c.Notes
                ))
                .ToListAsync();

            var result = new PaginatedList<ContactDto>(contacts, totalCount, query.PageNumber, query.PageSize);
            return Result.Ok(result);
        }
        catch (Exception ex)
        {
            return Result.Fail<PaginatedList<ContactDto>>($"Error retrieving contacts: {ex.Message}");
        }
    }

    private static IQueryable<Contact> ApplySorting(IQueryable<Contact> query, string? sortColumn, string? sortDirection)
    {
        var isDescending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return sortColumn?.ToLower() switch
        {
            "firstname" => isDescending ? query.OrderByDescending(c => c.FirstName) : query.OrderBy(c => c.FirstName),
            "lastname" => isDescending ? query.OrderByDescending(c => c.LastName) : query.OrderBy(c => c.LastName),
            "fullname" or "name" => isDescending
                ? query.OrderByDescending(c => c.FirstName + " " + c.LastName)
                : query.OrderBy(c => c.FirstName + " " + c.LastName),
            "email" => isDescending ? query.OrderByDescending(c => c.Email) : query.OrderBy(c => c.Email),
            "jobtitle" => isDescending ? query.OrderByDescending(c => c.JobTitle) : query.OrderBy(c => c.JobTitle),
            "companyname" => isDescending
                ? query.OrderByDescending(c => c.Company != null ? c.Company.CompanyName : "")
                : query.OrderBy(c => c.Company != null ? c.Company.CompanyName : ""),
            "isprimarycontact" => isDescending ? query.OrderByDescending(c => c.IsPrimaryContact) : query.OrderBy(c => c.IsPrimaryContact),
            "isactive" => isDescending ? query.OrderByDescending(c => c.IsActive) : query.OrderBy(c => c.IsActive),
            _ => query.OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
        };
    }

    public async Task<Result<ContactDto>> GetByIdAsync(Guid id)
    {
        try
        {
            var contact = await _context.Contacts
                .Include(c => c.Company)
                .Include(c => c.Site)
                .Where(c => c.Id == id)
                .Select(c => new ContactDto(
                    c.Id,
                    c.FirstName,
                    c.LastName,
                    c.FirstName + " " + c.LastName,
                    c.JobTitle,
                    c.Email,
                    c.Phone,
                    c.Mobile,
                    c.CompanyId,
                    c.Company != null ? c.Company.CompanyName : null,
                    c.SiteId,
                    c.Site != null ? c.Site.SiteName : null,
                    c.IsPrimaryContact,
                    c.IsActive,
                    c.Notes
                ))
                .FirstOrDefaultAsync();

            if (contact == null)
            {
                return Result.Fail<ContactDto>($"Contact with ID {id} not found");
            }

            return Result.Ok(contact);
        }
        catch (Exception ex)
        {
            return Result.Fail<ContactDto>($"Error retrieving contact: {ex.Message}");
        }
    }

    public async Task<Result<ContactDto>> CreateAsync(CreateContactDto dto)
    {
        try
        {
            // Validate that CompanyId exists if provided
            if (dto.CompanyId.HasValue)
            {
                var companyExists = await _context.Companies
                    .AnyAsync(c => c.Id == dto.CompanyId.Value);

                if (!companyExists)
                {
                    return Result.Fail<ContactDto>($"Company with ID {dto.CompanyId} not found");
                }
            }

            // Validate that SiteId exists if provided
            if (dto.SiteId.HasValue)
            {
                var siteExists = await _context.Sites
                    .AnyAsync(s => s.Id == dto.SiteId.Value);

                if (!siteExists)
                {
                    return Result.Fail<ContactDto>($"Site with ID {dto.SiteId} not found");
                }
            }

            // If setting as primary contact, unset existing primary contacts for this company
            if (dto.IsPrimaryContact && dto.CompanyId.HasValue)
            {
                var existingPrimaryContacts = await _context.Contacts
                    .Where(c => c.CompanyId == dto.CompanyId.Value && c.IsPrimaryContact)
                    .ToListAsync();

                foreach (var existingContact in existingPrimaryContacts)
                {
                    existingContact.IsPrimaryContact = false;
                }
            }

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                JobTitle = dto.JobTitle,
                Email = dto.Email,
                Phone = dto.Phone,
                Mobile = dto.Mobile,
                CompanyId = dto.CompanyId,
                SiteId = dto.SiteId,
                IsPrimaryContact = dto.IsPrimaryContact,
                IsActive = dto.IsActive,
                Notes = dto.Notes
            };

            _context.Contacts.Add(contact);
            await _context.SaveChangesAsync();

            // Reload with related entities
            var createdContact = await _context.Contacts
                .Include(c => c.Company)
                .Include(c => c.Site)
                .FirstAsync(c => c.Id == contact.Id);

            var contactDto = new ContactDto(
                createdContact.Id,
                createdContact.FirstName,
                createdContact.LastName,
                createdContact.FirstName + " " + createdContact.LastName,
                createdContact.JobTitle,
                createdContact.Email,
                createdContact.Phone,
                createdContact.Mobile,
                createdContact.CompanyId,
                createdContact.Company?.CompanyName,
                createdContact.SiteId,
                createdContact.Site?.SiteName,
                createdContact.IsPrimaryContact,
                createdContact.IsActive,
                createdContact.Notes
            );

            return Result.Ok(contactDto);
        }
        catch (Exception ex)
        {
            return Result.Fail<ContactDto>($"Error creating contact: {ex.Message}");
        }
    }

    public async Task<Result<ContactDto>> UpdateAsync(Guid id, UpdateContactDto dto)
    {
        try
        {
            var contact = await _context.Contacts
                .Include(c => c.Company)
                .Include(c => c.Site)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (contact == null)
            {
                return Result.Fail<ContactDto>($"Contact with ID {id} not found");
            }

            // Validate that CompanyId exists if provided
            if (dto.CompanyId.HasValue)
            {
                var companyExists = await _context.Companies
                    .AnyAsync(c => c.Id == dto.CompanyId.Value);

                if (!companyExists)
                {
                    return Result.Fail<ContactDto>($"Company with ID {dto.CompanyId} not found");
                }
            }

            // Validate that SiteId exists if provided
            if (dto.SiteId.HasValue)
            {
                var siteExists = await _context.Sites
                    .AnyAsync(s => s.Id == dto.SiteId.Value);

                if (!siteExists)
                {
                    return Result.Fail<ContactDto>($"Site with ID {dto.SiteId} not found");
                }
            }

            // If setting as primary contact, unset existing primary contacts for this company
            if (dto.IsPrimaryContact && dto.CompanyId.HasValue)
            {
                var existingPrimaryContacts = await _context.Contacts
                    .Where(c => c.CompanyId == dto.CompanyId.Value && c.IsPrimaryContact && c.Id != id)
                    .ToListAsync();

                foreach (var existingContact in existingPrimaryContacts)
                {
                    existingContact.IsPrimaryContact = false;
                }
            }

            contact.FirstName = dto.FirstName;
            contact.LastName = dto.LastName;
            contact.JobTitle = dto.JobTitle;
            contact.Email = dto.Email;
            contact.Phone = dto.Phone;
            contact.Mobile = dto.Mobile;
            contact.CompanyId = dto.CompanyId;
            contact.SiteId = dto.SiteId;
            contact.IsPrimaryContact = dto.IsPrimaryContact;
            contact.IsActive = dto.IsActive;
            contact.Notes = dto.Notes;

            await _context.SaveChangesAsync();

            // Reload to get updated related entity names
            await _context.Contacts
                .Entry(contact)
                .Reference(c => c.Company)
                .LoadAsync();
            await _context.Contacts
                .Entry(contact)
                .Reference(c => c.Site)
                .LoadAsync();

            var contactDto = new ContactDto(
                contact.Id,
                contact.FirstName,
                contact.LastName,
                contact.FirstName + " " + contact.LastName,
                contact.JobTitle,
                contact.Email,
                contact.Phone,
                contact.Mobile,
                contact.CompanyId,
                contact.Company?.CompanyName,
                contact.SiteId,
                contact.Site?.SiteName,
                contact.IsPrimaryContact,
                contact.IsActive,
                contact.Notes
            );

            return Result.Ok(contactDto);
        }
        catch (Exception ex)
        {
            return Result.Fail<ContactDto>($"Error updating contact: {ex.Message}");
        }
    }

    public async Task<Result> DeleteAsync(Guid id)
    {
        try
        {
            var contact = await _context.Contacts
                .FirstOrDefaultAsync(c => c.Id == id);

            if (contact == null)
            {
                return Result.Fail($"Contact with ID {id} not found");
            }

            contact.IsDeleted = true;
            await _context.SaveChangesAsync();

            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Error deleting contact: {ex.Message}");
        }
    }
}
