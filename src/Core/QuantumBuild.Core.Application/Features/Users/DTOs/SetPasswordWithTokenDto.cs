namespace QuantumBuild.Core.Application.Features.Users.DTOs;

/// <summary>
/// DTO for setting a password using an email verification token.
/// Used when new users receive an email with a password setup link.
/// </summary>
public record SetPasswordWithTokenDto(
    string Email,
    string Token,
    string NewPassword,
    string ConfirmPassword
);
