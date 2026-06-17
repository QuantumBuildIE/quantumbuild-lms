using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Abstractions.Email;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services;

/// <summary>
/// Sends admin notification emails for translation and validation pipeline events.
/// All methods swallow exceptions so a notification failure never breaks the calling operation.
/// </summary>
public class ToolboxTalkNotificationService : IToolboxTalkNotificationService
{
    private readonly IToolboxTalksDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly IEmailProvider _emailProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ToolboxTalkNotificationService> _logger;

    public ToolboxTalkNotificationService(
        IToolboxTalksDbContext context,
        UserManager<User> userManager,
        IEmailProvider emailProvider,
        IConfiguration configuration,
        ILogger<ToolboxTalkNotificationService> logger)
    {
        _context = context;
        _userManager = userManager;
        _emailProvider = emailProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task NotifyTranslationCompleteAsync(
        Guid tenantId,
        Guid talkId,
        string talkTitle,
        IReadOnlyList<TranslationLanguageResult> results,
        CancellationToken ct = default)
    {
        try
        {
            var settings = await GetSettingsAsync(tenantId, ct);
            if (settings is not null && !settings.NotifyOnTranslationComplete)
                return;

            if (results.Count == 0)
                return;

            var admins = await GetTenantAdminsAsync(tenantId);
            if (admins.Count == 0)
                return;

            var successCount = results.Count(r => r.Success);
            var failCount = results.Count(r => !r.Success);
            var baseUrl = GetBaseUrl();
            var talkUrl = $"{baseUrl}/admin/toolbox-talks/talks/{talkId}";
            var statusText = failCount == 0 ? "Completed" : $"Completed with {failCount} failure(s)";

            var languageRows = string.Join("", results.Select(r =>
            {
                var icon = r.Success ? "✔" : "✘";
                var colour = r.Success ? "#28a745" : "#dc3545";
                var detail = r.Success ? "Translated successfully" : (r.ErrorMessage ?? "Failed");
                return $@"<tr>
                    <td style=""padding:6px 12px;border-bottom:1px solid #eee;"">{r.LanguageName}</td>
                    <td style=""padding:6px 12px;border-bottom:1px solid #eee;color:{colour};font-weight:bold;"">{icon} {detail}</td>
                </tr>";
            }));

            var subject = $"Translation {statusText}: {talkTitle}";
            var body = BuildEmailBody(
                heading: $"Translation {statusText}",
                intro: $"The content translation job for <strong>{talkTitle}</strong> has finished.",
                details: $@"
                    <p><strong>Result:</strong> {successCount} succeeded, {failCount} failed</p>
                    <table style=""width:100%;border-collapse:collapse;margin-top:12px;"">
                        <thead>
                            <tr style=""background:#f5f5f5;"">
                                <th style=""padding:6px 12px;text-align:left;"">Language</th>
                                <th style=""padding:6px 12px;text-align:left;"">Status</th>
                            </tr>
                        </thead>
                        <tbody>{languageRows}</tbody>
                    </table>",
                ctaUrl: talkUrl,
                ctaText: "View Talk");

            await SendToAdminsAsync(admins, subject, body, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NotifyTranslationCompleteAsync failed for talk {TalkId} — notification suppressed", talkId);
        }
    }

    public async Task NotifyValidationCompleteAsync(
        Guid tenantId,
        Guid talkId,
        string talkTitle,
        string languageName,
        string outcome,
        double? score,
        int passedSections,
        int totalSections,
        CancellationToken ct = default)
    {
        try
        {
            var settings = await GetSettingsAsync(tenantId, ct);
            if (settings is not null && !settings.NotifyOnValidationComplete)
                return;

            var admins = await GetTenantAdminsAsync(tenantId);
            if (admins.Count == 0)
                return;

            var baseUrl = GetBaseUrl();
            var talkUrl = $"{baseUrl}/admin/toolbox-talks/talks/{talkId}/validation";
            var outcomeColour = outcome.ToUpperInvariant() switch
            {
                "PASS" => "#28a745",
                "FAIL" => "#dc3545",
                _ => "#fd7e14"
            };
            var scoreText = score.HasValue ? $"{score.Value:F1}%" : "N/A";

            var subject = $"Validation {outcome}: {talkTitle} ({languageName})";
            var body = BuildEmailBody(
                heading: $"Validation {outcome}",
                intro: $"The translation validation run for <strong>{talkTitle}</strong> ({languageName}) has completed.",
                details: $@"
                    <table style=""width:100%;border-collapse:collapse;"">
                        <tr><td style=""padding:6px 0;""><strong>Language:</strong></td><td>{languageName}</td></tr>
                        <tr><td style=""padding:6px 0;""><strong>Outcome:</strong></td>
                            <td style=""color:{outcomeColour};font-weight:bold;"">{outcome.ToUpperInvariant()}</td></tr>
                        <tr><td style=""padding:6px 0;""><strong>Score:</strong></td><td>{scoreText}</td></tr>
                        <tr><td style=""padding:6px 0;""><strong>Sections:</strong></td><td>{passedSections} / {totalSections} passed</td></tr>
                    </table>",
                ctaUrl: talkUrl,
                ctaText: "View Validation");

            await SendToAdminsAsync(admins, subject, body, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NotifyValidationCompleteAsync failed for talk {TalkId} — notification suppressed", talkId);
        }
    }

    public async Task NotifyFailureAsync(
        Guid tenantId,
        Guid talkId,
        string talkTitle,
        string failureContext,
        string errorMessage,
        CancellationToken ct = default)
    {
        try
        {
            var settings = await GetSettingsAsync(tenantId, ct);
            if (settings is not null && !settings.NotifyOnFailure)
                return;

            var admins = await GetTenantAdminsAsync(tenantId);
            if (admins.Count == 0)
                return;

            var baseUrl = GetBaseUrl();
            var talkUrl = $"{baseUrl}/admin/toolbox-talks/talks/{talkId}";

            var subject = $"Pipeline Failure: {talkTitle} — {failureContext}";
            var body = BuildEmailBody(
                heading: "Pipeline Failure",
                intro: $"A pipeline job for <strong>{talkTitle}</strong> has failed and requires attention.",
                details: $@"
                    <table style=""width:100%;border-collapse:collapse;"">
                        <tr><td style=""padding:6px 0;""><strong>Context:</strong></td><td>{System.Net.WebUtility.HtmlEncode(failureContext)}</td></tr>
                        <tr><td style=""padding:6px 0;vertical-align:top;""><strong>Error:</strong></td>
                            <td style=""color:#dc3545;"">{System.Net.WebUtility.HtmlEncode(errorMessage)}</td></tr>
                    </table>",
                ctaUrl: talkUrl,
                ctaText: "View Talk");

            await SendToAdminsAsync(admins, subject, body, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NotifyFailureAsync failed for talk {TalkId} — notification suppressed", talkId);
        }
    }

    public async Task NotifyExternalReviewResponseAsync(
        Guid tenantId,
        Guid talkId,
        string talkTitle,
        string languageName,
        bool accepted,
        CancellationToken ct = default)
    {
        try
        {
            var settings = await GetSettingsAsync(tenantId, ct);
            if (settings is not null && !settings.NotifyOnExternalReviewResponse)
                return;

            var admins = await GetTenantAdminsAsync(tenantId);
            if (admins.Count == 0)
                return;

            var baseUrl = GetBaseUrl();
            var talkUrl = $"{baseUrl}/admin/toolbox-talks/talks/{talkId}/validation";
            var responseText = accepted ? "Accepted" : "Rejected";
            var responseColour = accepted ? "#28a745" : "#dc3545";

            var subject = $"External Review {responseText}: {talkTitle} ({languageName})";
            var body = BuildEmailBody(
                heading: $"External Review {responseText}",
                intro: $"An external reviewer has responded to the review request for <strong>{talkTitle}</strong> ({languageName}).",
                details: $@"
                    <table style=""width:100%;border-collapse:collapse;"">
                        <tr><td style=""padding:6px 0;""><strong>Language:</strong></td><td>{languageName}</td></tr>
                        <tr><td style=""padding:6px 0;""><strong>Response:</strong></td>
                            <td style=""color:{responseColour};font-weight:bold;"">{responseText}</td></tr>
                    </table>",
                ctaUrl: talkUrl,
                ctaText: "View Validation");

            await SendToAdminsAsync(admins, subject, body, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NotifyExternalReviewResponseAsync failed for talk {TalkId} — notification suppressed", talkId);
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private async Task<QuantumBuild.Modules.ToolboxTalks.Domain.Entities.ToolboxTalkSettings?> GetSettingsAsync(
        Guid tenantId, CancellationToken ct)
    {
        return await _context.ToolboxTalkSettings
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<List<User>> GetTenantAdminsAsync(Guid tenantId)
    {
        var admins = await _userManager.GetUsersInRoleAsync("Admin");
        return admins
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .Where(u => !string.IsNullOrEmpty(u.Email))
            .ToList();
    }

    private async Task SendToAdminsAsync(
        IEnumerable<User> admins, string subject, string htmlBody, CancellationToken ct)
    {
        foreach (var admin in admins)
        {
            var message = new EmailMessage
            {
                ToEmail = admin.Email!,
                ToName = admin.FullName,
                Subject = subject,
                HtmlBody = htmlBody
            };
            var result = await _emailProvider.SendAsync(message, ct);
            if (!result.Success)
            {
                _logger.LogWarning(
                    "Failed to send notification to admin {Email}: {Error}",
                    admin.Email, result.ErrorMessage);
            }
        }
    }

    private string GetBaseUrl() =>
        _configuration["AppSettings:BaseUrl"] ?? "https://certifiediq.ai";

    private static string BuildEmailBody(
        string heading, string intro, string details, string ctaUrl, string ctaText) => $@"
<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""></head>
<body style=""font-family:Arial,sans-serif;margin:0;padding:0;background:#f4f4f4;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f4f4f4;padding:20px 0;"">
    <tr>
      <td align=""center"">
        <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background:#ffffff;border-radius:8px;overflow:hidden;"">
          <tr>
            <td style=""background:#16a34a;padding:24px 32px;"">
              <h1 style=""margin:0;color:#ffffff;font-size:22px;"">CertifiedIQ</h1>
            </td>
          </tr>
          <tr>
            <td style=""padding:32px;"">
              <h2 style=""margin:0 0 16px;color:#111827;font-size:18px;"">{heading}</h2>
              <p style=""margin:0 0 16px;color:#374151;line-height:1.6;"">{intro}</p>
              <div style=""margin:16px 0;color:#374151;line-height:1.6;"">
                {details}
              </div>
              <p style=""margin:24px 0 0;"">
                <a href=""{ctaUrl}"" style=""background:#16a34a;color:#fff;padding:12px 24px;text-decoration:none;border-radius:6px;font-weight:bold;display:inline-block;"">{ctaText}</a>
              </p>
            </td>
          </tr>
          <tr>
            <td style=""background:#f9fafb;padding:16px 32px;border-top:1px solid #e5e7eb;"">
              <p style=""margin:0;color:#6b7280;font-size:12px;"">This is an automated notification from CertifiedIQ. Do not reply to this email.</p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
}
