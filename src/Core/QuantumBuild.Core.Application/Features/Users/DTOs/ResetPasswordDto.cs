namespace QuantumBuild.Core.Application.Features.Users.DTOs;

public record ResetPasswordDto(
    string NewPassword,
    string ConfirmPassword
);
