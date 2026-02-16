namespace QuantumBuild.Core.Infrastructure.Identity;

/// <summary>
/// Configuration settings for JWT authentication
/// </summary>
public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    /// <summary>
    /// Secret key for signing JWT tokens
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// JWT token issuer
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// JWT token audience
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Access token expiry in minutes
    /// </summary>
    public int ExpiryMinutes { get; set; } = 60;

    /// <summary>
    /// Refresh token expiry in days
    /// </summary>
    public int RefreshTokenExpiryDays { get; set; } = 7;
}
