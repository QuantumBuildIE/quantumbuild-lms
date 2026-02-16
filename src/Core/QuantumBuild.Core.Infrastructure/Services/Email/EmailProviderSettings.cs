namespace QuantumBuild.Core.Infrastructure.Services.Email;

public class EmailProviderSettings
{
    public const string SectionName = "EmailProvider";

    public string Provider { get; set; } = "Stub";
    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "noreply@quantumbuild.ie";
    public string FromName { get; set; } = "QuantumBuild LMS";
}
