using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using QuantumBuild.Core.Application.Constants;
using QuantumBuild.Core.Application.DTOs.Auth;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Domain;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Infrastructure.Identity;

/// <summary>
/// Implementation of authentication service using ASP.NET Identity and JWT
/// </summary>
public class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;
    private readonly JwtSettings _jwtSettings;
    private readonly ICoreDbContext _db;
    private readonly ILogger<AuthService> _logger;
    private readonly ISystemAuditLogger _auditLogger;

    public AuthService(
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        IOptions<JwtSettings> jwtSettings,
        ICoreDbContext db,
        ILogger<AuthService> logger,
        ISystemAuditLogger auditLogger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _jwtSettings = jwtSettings.Value;
        _db = db;
        _logger = logger;
        _auditLogger = auditLogger;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress = null)
    {
        // Use Users queryable to ensure all custom properties (including EmployeeId) are loaded
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == request.Email.ToUpperInvariant());
        if (user == null)
        {
            await _auditLogger.LogAsync(
                AuditActions.Auth.LoginFailed,
                success: false,
                failureReason: "User not found",
                ipAddress: ipAddress);
            return AuthResponse.Failure("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            await _auditLogger.LogAsync(
                AuditActions.Auth.LoginAccountInactive,
                success: false,
                entityType: "User",
                entityId: user.Id,
                entityDisplayName: user.FullName,
                failureReason: "Account inactive",
                ipAddress: ipAddress);
            return AuthResponse.Failure("This account has been deactivated.");
        }

        var isValidPassword = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isValidPassword)
        {
            await _auditLogger.LogAsync(
                AuditActions.Auth.LoginFailed,
                success: false,
                entityType: "User",
                entityId: user.Id,
                entityDisplayName: user.FullName,
                failureReason: "Invalid password",
                ipAddress: ipAddress);
            await _userManager.AccessFailedAsync(user);
            return AuthResponse.Failure("Invalid email or password.");
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        user.LastLoginIp = ipAddress;
        await _userManager.UpdateAsync(user);

        await _auditLogger.LogAsync(
            AuditActions.Auth.LoginSuccess,
            success: true,
            entityType: "User",
            entityId: user.Id,
            entityDisplayName: user.FullName,
            ipAddress: ipAddress);

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return AuthResponse.Failure("A user with this email already exists.");
        }

        var user = new User
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            TenantId = request.TenantId,
            IsActive = true,
            EmailConfirmed = true // For development; should be false in production
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return AuthResponse.Failure(result.Errors.Select(e => e.Description));
        }

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var principal = GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal == null)
        {
            await _auditLogger.LogAsync(
                AuditActions.Auth.TokenRefresh,
                success: false,
                failureReason: "Invalid access token");
            return AuthResponse.Failure("Invalid access token.");
        }

        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            await _auditLogger.LogAsync(
                AuditActions.Auth.TokenRefresh,
                success: false,
                failureReason: "Invalid access token");
            return AuthResponse.Failure("Invalid access token.");
        }

        // Use Users queryable to ensure all custom properties (including EmployeeId) are loaded
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null || user.RefreshToken != request.RefreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            await _auditLogger.LogAsync(
                AuditActions.Auth.TokenRefresh,
                success: false,
                entityType: user != null ? "User" : null,
                entityId: user?.Id,
                entityDisplayName: user?.FullName,
                failureReason: "Invalid or expired refresh token");
            return AuthResponse.Failure("Invalid or expired refresh token.");
        }

        if (!user.IsActive)
        {
            await _auditLogger.LogAsync(
                AuditActions.Auth.TokenRefresh,
                success: false,
                entityType: "User",
                entityId: user.Id,
                entityDisplayName: user.FullName,
                failureReason: "Account inactive");
            return AuthResponse.Failure("This account has been deactivated.");
        }

        await _auditLogger.LogAsync(
            AuditActions.Auth.TokenRefresh,
            success: true,
            entityType: "User",
            entityId: user.Id,
            entityDisplayName: user.FullName);

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<bool> RevokeTokenAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return false;
        }

        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        await _userManager.UpdateAsync(user);

        await _auditLogger.LogAsync(
            AuditActions.Auth.Logout,
            success: true,
            entityType: "User",
            entityId: user.Id,
            entityDisplayName: user.FullName);

        return true;
    }

    public async Task<IEnumerable<string>> GetUserPermissionsAsync(Guid userId)
    {
        var user = await _userManager.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return Enumerable.Empty<string>();
        }

        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Name)
            .Distinct()
            .ToList();

        return permissions;
    }

    private async Task<AuthResponse> GenerateAuthResponseAsync(User user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetUserPermissionsAsync(user.Id);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName),
            new("tenant_id", user.TenantId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Add employee_id claim if user is linked to an employee
        if (user.EmployeeId.HasValue)
        {
            claims.Add(new Claim("employee_id", user.EmployeeId.Value.ToString()));
        }

        // Add super user claim
        if (user.IsSuperUser)
        {
            claims.Add(new Claim("is_super_user", "true"));
        }

        // Add role claims
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // Add permission claims
        foreach (var permission in permissions)
        {
            claims.Add(new Claim("permission", permission));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = GenerateRefreshToken();

        // Save refresh token to user
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays);
        await _userManager.UpdateAsync(user);

        // Resolve enabled modules for the user's tenant
        var enabledModules = user.IsSuperUser
            ? ModuleNames.All
            : await _db.TenantModules
                .Where(m => m.TenantId == user.TenantId)
                .Select(m => m.ModuleName)
                .ToArrayAsync();

        var userInfo = new UserInfo(
            user.Id,
            user.Email!,
            user.FirstName,
            user.LastName,
            user.TenantId,
            roles,
            permissions,
            user.IsSuperUser,
            user.EmployeeId,
            enabledModules
        );

        return new AuthResponse(
            true,
            accessToken,
            refreshToken,
            expiresAt,
            userInfo,
            null
        );
    }

    private static string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret)),
            ValidateLifetime = false, // We want to get claims from expired tokens
            ValidIssuer = _jwtSettings.Issuer,
            ValidAudience = _jwtSettings.Audience
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);
            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }
            return principal;
        }
        catch
        {
            return null;
        }
    }
}
