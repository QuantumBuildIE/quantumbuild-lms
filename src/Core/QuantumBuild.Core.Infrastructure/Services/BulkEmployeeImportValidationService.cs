using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Features.BulkImport;
using QuantumBuild.Core.Application.Interfaces;

namespace QuantumBuild.Core.Infrastructure.Services;

public sealed class BulkEmployeeImportValidationService : IBulkEmployeeImportValidationService
{
    private readonly ICoreDbContext _context;

    // Only these two roles may be assigned through bulk import.
    private static readonly HashSet<string> AllowedRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Operator", "Supervisor" };

    // Deliberately permissive — rejects obvious non-addresses without false-positives on
    // valid international formats. The definitive validation is ASP.NET Identity on creation.
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    public BulkEmployeeImportValidationService(ICoreDbContext context)
    {
        _context = context;
    }

    public async Task<BulkImportValidationResult> ValidateAsync(
        Stream csvStream,
        CancellationToken cancellationToken = default)
    {
        // Pre-load lookup sets for uniqueness checks.
        // Employee query filter automatically scopes to the current tenant and excludes soft-deleted rows.
        var existingEmployeeEmails = await _context.Employees
            .Where(e => e.Email != null)
            .Select(e => e.Email!.ToLower())
            .ToHashSetAsync(cancellationToken);

        // Users are globally unique by NormalizedEmail (Identity invariant).
        // No query filter on Users — the check is intentionally cross-tenant.
        var existingUserNormalizedEmails = await _context.Users
            .Where(u => u.NormalizedEmail != null)
            .Select(u => u.NormalizedEmail!)
            .ToHashSetAsync(cancellationToken);

        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            HeaderValidated = null,     // do not throw on missing optional columns
            MissingFieldFound = null,   // return null for absent optional columns
            TrimOptions = TrimOptions.Trim,
            BadDataFound = null,        // do not throw on unquoted fields that contain quotes

            // Case-insensitive header matching: "FirstName", "firstname", "FIRSTNAME" are equivalent.
            PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant()
        };

        // leaveOpen: true — caller owns the stream lifecycle.
        using var reader = new StreamReader(csvStream, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        using var csv = new CsvReader(reader, csvConfig);

        // --- Header row ---
        if (!await csv.ReadAsync())
        {
            return StructuralError("The CSV file is empty.");
        }

        csv.ReadHeader();

        var rawHeaders = csv.HeaderRecord ?? Array.Empty<string>();
        var normalizedHeaders = rawHeaders
            .Select(h => h.Trim().ToLowerInvariant())
            .ToHashSet();

        var requiredColumns = new[] { "firstname", "lastname", "email", "createuseraccount" };
        var missingColumns = requiredColumns.Where(c => !normalizedHeaders.Contains(c)).ToList();
        if (missingColumns.Count > 0)
        {
            return StructuralError($"CSV is missing required column(s): {string.Join(", ", missingColumns)}.");
        }

        // --- Data rows ---
        var results = new List<BulkImportRowResult>();

        // Track emails seen within this file to catch intra-CSV duplicates.
        var seenEmailsInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Row 1 = header; first data row is 2 (matches spreadsheet row numbers).
        var rowNumber = 1;

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;

            var messages = new List<string>();
            var status = BulkImportRowStatus.Valid;

            void Fail(string msg) { messages.Add(msg); status = BulkImportRowStatus.Failed; }
            void Warn(string msg) { messages.Add(msg); if (status == BulkImportRowStatus.Valid) status = BulkImportRowStatus.Warning; }

            // Read raw cell values (TrimOptions.Trim already strips whitespace).
            var firstName = NullIfEmpty(csv.GetField<string?>("firstname"));
            var lastName  = NullIfEmpty(csv.GetField<string?>("lastname"));
            var email     = NullIfEmpty(csv.GetField<string?>("email"));
            var createUserAccountRaw = NullIfEmpty(csv.GetField<string?>("createuseraccount"));
            var phone             = NullIfEmpty(csv.GetField<string?>("phone"));
            var mobile            = NullIfEmpty(csv.GetField<string?>("mobile"));
            var jobTitle          = NullIfEmpty(csv.GetField<string?>("jobtitle"));
            var department        = NullIfEmpty(csv.GetField<string?>("department"));
            var startDateRaw      = NullIfEmpty(csv.GetField<string?>("startdate"));
            var endDateRaw        = NullIfEmpty(csv.GetField<string?>("enddate"));
            var notes             = NullIfEmpty(csv.GetField<string?>("notes"));
            var preferredLangRaw  = NullIfEmpty(csv.GetField<string?>("preferredlanguage"));
            var userRoleRaw       = NullIfEmpty(csv.GetField<string?>("userrole"));

            // Required text fields
            if (firstName is null) Fail("FirstName is required.");
            else if (firstName.Length > 100) Fail("FirstName exceeds 100 characters.");
            if (lastName is null)  Fail("LastName is required.");
            else if (lastName.Length > 100) Fail("LastName exceeds 100 characters.");

            // CreateUserAccount — blank → true (default), Yes/No case-insensitive, anything else → fail
            bool createUserAccount = true;
            bool createUserAccountValid = true;
            if (createUserAccountRaw is null)
            {
                createUserAccount = true;
            }
            else if (createUserAccountRaw.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                createUserAccount = true;
            }
            else if (createUserAccountRaw.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                createUserAccount = false;
            }
            else
            {
                Fail($"CreateUserAccount must be 'Yes' or 'No' (got '{createUserAccountRaw}').");
                createUserAccountValid = false;
            }

            // Email — required, valid format, unique in file, unique in tenant employees,
            // and (when CreateUserAccount=Yes) unique globally across user accounts.
            if (email is null)
            {
                Fail("Email is required.");
            }
            else if (email.Length > 200)
            {
                Fail("Email exceeds 200 characters.");
            }
            else if (!EmailRegex.IsMatch(email))
            {
                Fail($"'{email}' is not a valid email address.");
            }
            else if (!seenEmailsInFile.Add(email))
            {
                Fail($"Email '{email}' appears more than once in this file.");
            }
            else
            {
                if (existingEmployeeEmails.Contains(email.ToLower()))
                    Fail($"An employee with email '{email}' already exists in this account.");

                if (createUserAccountValid && createUserAccount
                    && existingUserNormalizedEmails.Contains(email.ToUpperInvariant()))
                {
                    Fail($"A user account for '{email}' already exists in the system.");
                }
            }

            // StartDate / EndDate — YYYY-MM-DD only
            DateOnly? startDate = null;
            DateOnly? endDate = null;

            if (startDateRaw is not null)
            {
                if (DateOnly.TryParseExact(startDateRaw, "yyyy-MM-dd", null, DateTimeStyles.None, out var sd))
                    startDate = sd;
                else
                    Fail($"StartDate '{startDateRaw}' must be in YYYY-MM-DD format.");
            }

            if (endDateRaw is not null)
            {
                if (DateOnly.TryParseExact(endDateRaw, "yyyy-MM-dd", null, DateTimeStyles.None, out var ed))
                    endDate = ed;
                else
                    Fail($"EndDate '{endDateRaw}' must be in YYYY-MM-DD format.");
            }

            if (startDate.HasValue && endDate.HasValue && startDate >= endDate)
                Fail("StartDate must be before EndDate.");

            // Optional field length limits (must match CreateEmployeeValidator)
            if (phone      is not null && phone.Length      > 50)   Fail("Phone exceeds 50 characters.");
            if (mobile     is not null && mobile.Length     > 50)   Fail("Mobile exceeds 50 characters.");
            if (jobTitle   is not null && jobTitle.Length   > 100)  Fail("JobTitle exceeds 100 characters.");
            if (department is not null && department.Length > 100)  Fail("Department exceeds 100 characters.");
            if (notes      is not null && notes.Length      > 2000) Fail("Notes exceeds 2000 characters.");

            // UserRole — Operator or Supervisor only; blank/unrecognised → default Operator (not a failure)
            var userRole = "Operator";
            if (userRoleRaw is not null)
            {
                if (AllowedRoles.Contains(userRoleRaw))
                {
                    // Normalise casing to match the seeded role names.
                    userRole = userRoleRaw.Equals("supervisor", StringComparison.OrdinalIgnoreCase)
                        ? "Supervisor"
                        : "Operator";
                }
                // Unrecognised value silently defaults to Operator — not a failure per spec.
            }

            // Warning: role supplied but account won't be created — it will be ignored.
            if (userRoleRaw is not null && !createUserAccount)
                Warn("UserRole is set but CreateUserAccount is 'No' — the role will not be applied.");

            // PreferredLanguage — blank/unrecognised → "en"; accept any ≤10-char code without
            // validating against a fixed list (supported languages are tenant-configurable).
            var preferredLanguage = "en";
            if (preferredLangRaw is not null)
            {
                var code = preferredLangRaw.ToLowerInvariant();
                preferredLanguage = code.Length is >= 2 and <= 10 ? code : "en";
            }

            results.Add(new BulkImportRowResult
            {
                RowNumber       = rowNumber,
                Status          = status,
                Messages        = messages,
                FirstName       = firstName,
                LastName        = lastName,
                Email           = email,
                CreateUserAccount = createUserAccount,
                Phone           = phone,
                Mobile          = mobile,
                JobTitle        = jobTitle,
                Department      = department,
                StartDate       = startDate,
                EndDate         = endDate,
                Notes           = notes,
                PreferredLanguage = preferredLanguage,
                UserRole        = userRole
            });
        }

        return new BulkImportValidationResult
        {
            TotalRows    = results.Count,
            ValidRows    = results.Count(r => r.Status == BulkImportRowStatus.Valid),
            WarningRows  = results.Count(r => r.Status == BulkImportRowStatus.Warning),
            FailedRows   = results.Count(r => r.Status == BulkImportRowStatus.Failed),
            Rows         = results
        };
    }

    // Returns a result with a single structural-error row (RowNumber=0 = file-level error).
    private static BulkImportValidationResult StructuralError(string message) =>
        new()
        {
            TotalRows = 0,
            Rows = new List<BulkImportRowResult>
            {
                new()
                {
                    RowNumber = 0,
                    Status    = BulkImportRowStatus.Failed,
                    Messages  = new List<string> { message }
                }
            }
        };

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
