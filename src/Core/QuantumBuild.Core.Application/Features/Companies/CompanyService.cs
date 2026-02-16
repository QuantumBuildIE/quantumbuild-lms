using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Features.Companies.DTOs;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Application.Features.Companies;

public class CompanyService : ICompanyService
{
    private readonly ICoreDbContext _context;

    public CompanyService(ICoreDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<CompanyDto>>> GetAllAsync()
    {
        try
        {
            var companies = await _context.Companies
                .Include(c => c.Contacts)
                .OrderBy(c => c.CompanyCode)
                .Select(c => new CompanyDto(
                    c.Id,
                    c.CompanyCode,
                    c.CompanyName,
                    c.TradingName,
                    c.RegistrationNumber,
                    c.VatNumber,
                    c.AddressLine1,
                    c.AddressLine2,
                    c.City,
                    c.County,
                    c.PostalCode,
                    c.Country,
                    c.Phone,
                    c.Email,
                    c.Website,
                    c.CompanyType,
                    c.IsActive,
                    c.Notes,
                    c.Contacts.Count(ct => !ct.IsDeleted),
                    c.Contacts.Where(ct => !ct.IsDeleted).Select(ct => new ContactSummaryDto(
                        ct.Id,
                        ct.FirstName,
                        ct.LastName,
                        ct.FirstName + " " + ct.LastName,
                        ct.JobTitle,
                        ct.Email,
                        ct.Phone,
                        ct.Mobile,
                        ct.IsPrimaryContact,
                        ct.IsActive
                    )).ToList()
                ))
                .ToListAsync();

            return Result.Ok(companies);
        }
        catch (Exception ex)
        {
            return Result.Fail<List<CompanyDto>>($"Error retrieving companies: {ex.Message}");
        }
    }

    public async Task<Result<PaginatedList<CompanyDto>>> GetPaginatedAsync(GetCompaniesQueryDto query)
    {
        try
        {
            var companiesQuery = _context.Companies
                .Include(c => c.Contacts)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var searchLower = query.Search.ToLower();
                companiesQuery = companiesQuery.Where(c =>
                    c.CompanyCode.ToLower().Contains(searchLower) ||
                    c.CompanyName.ToLower().Contains(searchLower) ||
                    (c.TradingName != null && c.TradingName.ToLower().Contains(searchLower)) ||
                    (c.Email != null && c.Email.ToLower().Contains(searchLower)) ||
                    (c.Phone != null && c.Phone.ToLower().Contains(searchLower))
                );
            }

            // Apply company type filter
            if (!string.IsNullOrWhiteSpace(query.CompanyType))
            {
                companiesQuery = companiesQuery.Where(c => c.CompanyType == query.CompanyType);
            }

            // Apply sorting
            companiesQuery = ApplySorting(companiesQuery, query.SortColumn, query.SortDirection);

            // Get total count before pagination
            var totalCount = await companiesQuery.CountAsync();

            // Apply pagination
            var companies = await companiesQuery
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(c => new CompanyDto(
                    c.Id,
                    c.CompanyCode,
                    c.CompanyName,
                    c.TradingName,
                    c.RegistrationNumber,
                    c.VatNumber,
                    c.AddressLine1,
                    c.AddressLine2,
                    c.City,
                    c.County,
                    c.PostalCode,
                    c.Country,
                    c.Phone,
                    c.Email,
                    c.Website,
                    c.CompanyType,
                    c.IsActive,
                    c.Notes,
                    c.Contacts.Count(ct => !ct.IsDeleted),
                    c.Contacts.Where(ct => !ct.IsDeleted).Select(ct => new ContactSummaryDto(
                        ct.Id,
                        ct.FirstName,
                        ct.LastName,
                        ct.FirstName + " " + ct.LastName,
                        ct.JobTitle,
                        ct.Email,
                        ct.Phone,
                        ct.Mobile,
                        ct.IsPrimaryContact,
                        ct.IsActive
                    )).ToList()
                ))
                .ToListAsync();

            var result = new PaginatedList<CompanyDto>(companies, totalCount, query.PageNumber, query.PageSize);
            return Result.Ok(result);
        }
        catch (Exception ex)
        {
            return Result.Fail<PaginatedList<CompanyDto>>($"Error retrieving companies: {ex.Message}");
        }
    }

    private static IQueryable<Company> ApplySorting(IQueryable<Company> query, string? sortColumn, string? sortDirection)
    {
        var isDescending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return sortColumn?.ToLower() switch
        {
            "companycode" => isDescending ? query.OrderByDescending(c => c.CompanyCode) : query.OrderBy(c => c.CompanyCode),
            "companyname" => isDescending ? query.OrderByDescending(c => c.CompanyName) : query.OrderBy(c => c.CompanyName),
            "tradingname" => isDescending ? query.OrderByDescending(c => c.TradingName) : query.OrderBy(c => c.TradingName),
            "email" => isDescending ? query.OrderByDescending(c => c.Email) : query.OrderBy(c => c.Email),
            "phone" => isDescending ? query.OrderByDescending(c => c.Phone) : query.OrderBy(c => c.Phone),
            "companytype" => isDescending ? query.OrderByDescending(c => c.CompanyType) : query.OrderBy(c => c.CompanyType),
            "isactive" => isDescending ? query.OrderByDescending(c => c.IsActive) : query.OrderBy(c => c.IsActive),
            _ => query.OrderBy(c => c.CompanyCode)
        };
    }

    public async Task<Result<CompanyDto>> GetByIdAsync(Guid id)
    {
        try
        {
            var company = await _context.Companies
                .Include(c => c.Contacts)
                .Where(c => c.Id == id)
                .Select(c => new CompanyDto(
                    c.Id,
                    c.CompanyCode,
                    c.CompanyName,
                    c.TradingName,
                    c.RegistrationNumber,
                    c.VatNumber,
                    c.AddressLine1,
                    c.AddressLine2,
                    c.City,
                    c.County,
                    c.PostalCode,
                    c.Country,
                    c.Phone,
                    c.Email,
                    c.Website,
                    c.CompanyType,
                    c.IsActive,
                    c.Notes,
                    c.Contacts.Count(ct => !ct.IsDeleted),
                    c.Contacts.Where(ct => !ct.IsDeleted).Select(ct => new ContactSummaryDto(
                        ct.Id,
                        ct.FirstName,
                        ct.LastName,
                        ct.FirstName + " " + ct.LastName,
                        ct.JobTitle,
                        ct.Email,
                        ct.Phone,
                        ct.Mobile,
                        ct.IsPrimaryContact,
                        ct.IsActive
                    )).ToList()
                ))
                .FirstOrDefaultAsync();

            if (company == null)
            {
                return Result.Fail<CompanyDto>($"Company with ID {id} not found");
            }

            return Result.Ok(company);
        }
        catch (Exception ex)
        {
            return Result.Fail<CompanyDto>($"Error retrieving company: {ex.Message}");
        }
    }

    public async Task<Result<CompanyDto>> CreateAsync(CreateCompanyDto dto)
    {
        try
        {
            // Check for duplicate CompanyCode within the same tenant
            var duplicateCode = await _context.Companies
                .AnyAsync(c => c.CompanyCode == dto.CompanyCode);

            if (duplicateCode)
            {
                return Result.Fail<CompanyDto>($"Company with code '{dto.CompanyCode}' already exists");
            }

            var company = new Company
            {
                Id = Guid.NewGuid(),
                CompanyCode = dto.CompanyCode,
                CompanyName = dto.CompanyName,
                TradingName = dto.TradingName,
                RegistrationNumber = dto.RegistrationNumber,
                VatNumber = dto.VatNumber,
                AddressLine1 = dto.AddressLine1,
                AddressLine2 = dto.AddressLine2,
                City = dto.City,
                County = dto.County,
                PostalCode = dto.PostalCode,
                Country = dto.Country,
                Phone = dto.Phone,
                Email = dto.Email,
                Website = dto.Website,
                CompanyType = dto.CompanyType,
                IsActive = dto.IsActive,
                Notes = dto.Notes
            };

            _context.Companies.Add(company);
            await _context.SaveChangesAsync();

            var companyDto = new CompanyDto(
                company.Id,
                company.CompanyCode,
                company.CompanyName,
                company.TradingName,
                company.RegistrationNumber,
                company.VatNumber,
                company.AddressLine1,
                company.AddressLine2,
                company.City,
                company.County,
                company.PostalCode,
                company.Country,
                company.Phone,
                company.Email,
                company.Website,
                company.CompanyType,
                company.IsActive,
                company.Notes,
                0,
                new List<ContactSummaryDto>()
            );

            return Result.Ok(companyDto);
        }
        catch (Exception ex)
        {
            return Result.Fail<CompanyDto>($"Error creating company: {ex.Message}");
        }
    }

    public async Task<Result<CompanyDto>> UpdateAsync(Guid id, UpdateCompanyDto dto)
    {
        try
        {
            var company = await _context.Companies
                .Include(c => c.Contacts)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (company == null)
            {
                return Result.Fail<CompanyDto>($"Company with ID {id} not found");
            }

            // Check for duplicate CompanyCode (excluding current company)
            var duplicateCode = await _context.Companies
                .AnyAsync(c => c.CompanyCode == dto.CompanyCode && c.Id != id);

            if (duplicateCode)
            {
                return Result.Fail<CompanyDto>($"Company with code '{dto.CompanyCode}' already exists");
            }

            company.CompanyCode = dto.CompanyCode;
            company.CompanyName = dto.CompanyName;
            company.TradingName = dto.TradingName;
            company.RegistrationNumber = dto.RegistrationNumber;
            company.VatNumber = dto.VatNumber;
            company.AddressLine1 = dto.AddressLine1;
            company.AddressLine2 = dto.AddressLine2;
            company.City = dto.City;
            company.County = dto.County;
            company.PostalCode = dto.PostalCode;
            company.Country = dto.Country;
            company.Phone = dto.Phone;
            company.Email = dto.Email;
            company.Website = dto.Website;
            company.CompanyType = dto.CompanyType;
            company.IsActive = dto.IsActive;
            company.Notes = dto.Notes;

            await _context.SaveChangesAsync();

            var companyDto = new CompanyDto(
                company.Id,
                company.CompanyCode,
                company.CompanyName,
                company.TradingName,
                company.RegistrationNumber,
                company.VatNumber,
                company.AddressLine1,
                company.AddressLine2,
                company.City,
                company.County,
                company.PostalCode,
                company.Country,
                company.Phone,
                company.Email,
                company.Website,
                company.CompanyType,
                company.IsActive,
                company.Notes,
                company.Contacts.Count(ct => !ct.IsDeleted),
                company.Contacts.Where(ct => !ct.IsDeleted).Select(ct => new ContactSummaryDto(
                    ct.Id,
                    ct.FirstName,
                    ct.LastName,
                    ct.FirstName + " " + ct.LastName,
                    ct.JobTitle,
                    ct.Email,
                    ct.Phone,
                    ct.Mobile,
                    ct.IsPrimaryContact,
                    ct.IsActive
                )).ToList()
            );

            return Result.Ok(companyDto);
        }
        catch (Exception ex)
        {
            return Result.Fail<CompanyDto>($"Error updating company: {ex.Message}");
        }
    }

    public async Task<Result> DeleteAsync(Guid id)
    {
        try
        {
            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.Id == id);

            if (company == null)
            {
                return Result.Fail($"Company with ID {id} not found");
            }

            company.IsDeleted = true;
            await _context.SaveChangesAsync();

            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Error deleting company: {ex.Message}");
        }
    }
}
