using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.DTOs.Auth;
using QuantumBuild.Core.Application.Features.Users;
using QuantumBuild.Core.Application.Features.Users.DTOs;
using QuantumBuild.Core.Application.Interfaces;

namespace QuantumBuild.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;

    public AuthController(IAuthService authService, IUserService userService)
    {
        _authService = authService;
        _userService = userService;
    }

    /// <summary>
    /// Authenticate a user and return JWT tokens
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);

        if (!result.Success)
        {
            return Unauthorized(new { errors = result.Errors });
        }

        return Ok(result);
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);

        if (!result.Success)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return Ok(result);
    }

    /// <summary>
    /// Refresh an expired JWT token
    /// </summary>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request);

        if (!result.Success)
        {
            return Unauthorized(new { errors = result.Errors });
        }

        return Ok(result);
    }

    /// <summary>
    /// Revoke the current user's refresh token (logout)
    /// </summary>
    [HttpPost("revoke-token")]
    [Authorize]
    public async Task<IActionResult> RevokeToken()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return BadRequest(new { error = "Invalid user." });
        }

        var result = await _authService.RevokeTokenAsync(userId);

        if (!result)
        {
            return BadRequest(new { error = "Failed to revoke token." });
        }

        return Ok(new { message = "Token revoked successfully." });
    }

    /// <summary>
    /// Get current user's information
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return BadRequest(new { error = "Invalid user." });
        }

        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var firstName = User.FindFirst(ClaimTypes.GivenName)?.Value;
        var lastName = User.FindFirst(ClaimTypes.Surname)?.Value;
        var tenantId = User.FindFirst("tenant_id")?.Value;
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value);
        var permissions = await _authService.GetUserPermissionsAsync(userId);

        return Ok(new
        {
            id = userId,
            email,
            firstName,
            lastName,
            tenantId,
            roles,
            permissions
        });
    }

    /// <summary>
    /// Set password for a new user using the token sent in their welcome email.
    /// This is a public endpoint - no authentication required.
    /// </summary>
    [HttpPost("set-password")]
    [AllowAnonymous]
    public async Task<IActionResult> SetPasswordWithToken([FromBody] SetPasswordWithTokenDto dto)
    {
        var result = await _userService.SetPasswordWithTokenAsync(dto);

        if (!result.Success)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return Ok(new { message = "Password set successfully. You can now log in." });
    }
}
