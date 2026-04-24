using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Abstractions;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Infrastructure.Services;

public class EmployeePinService : IEmployeePinService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly IPasswordHasher<Employee> _hasher;
    private readonly ICoreDbContext _context;
    private readonly ILogger<EmployeePinService> _logger;

    public EmployeePinService(
        IPasswordHasher<Employee> hasher,
        ICoreDbContext context,
        ILogger<EmployeePinService> logger)
    {
        _hasher = hasher;
        _context = context;
        _logger = logger;
    }

    public string GenerateRawPin()
    {
        // Generate a cryptographically random 6-digit PIN that is not 000000
        // and does not start with 000.
        while (true)
        {
            // RandomNumberGenerator gives us values in [0, 999999]
            var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
            var pin = value.ToString("D6");

            if (pin == "000000" || pin.StartsWith("000"))
                continue;

            return pin;
        }
    }

    public async Task<string> SetPinAsync(Employee employee, string rawPin, CancellationToken ct = default)
    {
        employee.QrPin = _hasher.HashPassword(employee, rawPin);
        employee.QrPinIsSet = true;
        employee.QrPinGeneratedAt = DateTimeOffset.UtcNow;
        employee.QrPinFailedAttempts = 0;
        employee.QrPinLockedUntil = null;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "PIN set for Employee {EmployeeId}", employee.Id);

        return rawPin;
    }

    public async Task<PinVerificationResult> VerifyPinAsync(
        Employee employee, string rawPin, CancellationToken ct = default)
    {
        if (employee.QrPinLockedUntil.HasValue && employee.QrPinLockedUntil.Value > DateTimeOffset.UtcNow)
        {
            return new PinVerificationResult(
                PinVerificationStatus.Locked,
                AttemptsRemaining: 0,
                LockedUntil: employee.QrPinLockedUntil);
        }

        if (!employee.QrPinIsSet || employee.QrPin is null)
        {
            return new PinVerificationResult(
                PinVerificationStatus.Failed,
                AttemptsRemaining: MaxFailedAttempts - 1,
                LockedUntil: null);
        }

        var result = _hasher.VerifyHashedPassword(employee, employee.QrPin, rawPin);

        if (result == PasswordVerificationResult.Success ||
            result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            employee.QrPinLastUsedAt = DateTimeOffset.UtcNow;
            employee.QrPinFailedAttempts = 0;
            employee.QrPinLockedUntil = null;

            await _context.SaveChangesAsync(ct);

            return new PinVerificationResult(
                PinVerificationStatus.Success,
                AttemptsRemaining: MaxFailedAttempts,
                LockedUntil: null);
        }

        // Failed verification
        employee.QrPinFailedAttempts++;

        if (employee.QrPinFailedAttempts >= MaxFailedAttempts)
        {
            employee.QrPinLockedUntil = DateTimeOffset.UtcNow.Add(LockoutDuration);
            _logger.LogWarning(
                "Employee {EmployeeId} PIN locked until {LockoutUntil} after {Attempts} failed attempts",
                employee.Id, employee.QrPinLockedUntil, employee.QrPinFailedAttempts);
        }

        await _context.SaveChangesAsync(ct);

        var attemptsRemaining = Math.Max(0, MaxFailedAttempts - employee.QrPinFailedAttempts);

        return new PinVerificationResult(
            PinVerificationStatus.Failed,
            AttemptsRemaining: attemptsRemaining,
            LockedUntil: employee.QrPinLockedUntil);
    }

    public async Task<string> ResetPinAsync(Employee employee, CancellationToken ct = default)
    {
        var rawPin = GenerateRawPin();
        await SetPinAsync(employee, rawPin, ct);
        return rawPin;
    }
}
