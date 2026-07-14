using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Abstractions.Email;
using QuantumBuild.Core.Application.Interfaces;

namespace QuantumBuild.Core.Infrastructure.Services;

/// <summary>
/// Email service for sending notifications.
/// Uses the centralized IEmailProvider (MailerSend, SendGrid, SMTP, etc.) for actual email delivery.
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly IEmailProvider _emailProvider;

    public EmailService(
        IConfiguration configuration,
        ILogger<EmailService> logger,
        IEmailProvider emailProvider)
    {
        _configuration = configuration;
        _logger = logger;
        _emailProvider = emailProvider;
    }

    public async Task SendPasswordSetupEmailAsync(
        string email,
        string firstName,
        string resetToken,
        string? qrPin = null,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://quantumbuild-lms-web-production.up.railway.app";
        var encodedToken = WebUtility.UrlEncode(resetToken);
        var encodedEmail = WebUtility.UrlEncode(email);
        var resetUrl = $"{baseUrl}/auth/set-password?email={encodedEmail}&token={encodedToken}";

        var pinSection = qrPin is not null ? BuildPinSection(qrPin) : string.Empty;

        var subject = "Welcome to CertifiedIQ - Set Up Your Account";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #28a745; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .button {{ display: inline-block; background-color: #28a745; color: white; padding: 12px 25px; text-decoration: none; border-radius: 5px; margin-top: 15px; }}
        .footer {{ padding: 20px; text-align: center; color: #666; font-size: 12px; }}
        .warning {{ background-color: #fff3cd; border: 1px solid #ffc107; padding: 10px; border-radius: 5px; margin-top: 15px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Welcome to CertifiedIQ</h1>
        </div>
        <div class='content'>
            <p>Dear {firstName},</p>
            <p>An account has been created for you in the CertifiedIQ Business Suite. Please click the link below to set your password and activate your account:</p>
            <p style='text-align: center;'>
                <a href='{resetUrl}' class='button'>Set Up My Password</a>
            </p>
            <div class='warning'>
                <strong>Important:</strong> This link will expire in 24 hours. If you did not expect this email, please contact your administrator.
            </div>
            <p style='margin-top: 20px;'>If the button doesn't work, copy and paste this link into your browser:</p>
            <p style='word-break: break-all; font-size: 12px; color: #666;'>{resetUrl}</p>
            {pinSection}
        </div>
        <div class='footer'>
            <p>Thank you,<br>The CertifiedIQ Team</p>
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(email, subject, body, cancellationToken);
    }

    /// <summary>
    /// Sends a standalone PIN notification email (no password link).
    /// Used for PIN resets and batch introduction emails.
    /// </summary>
    public async Task SendPinEmailAsync(
        string email,
        string firstName,
        string qrPin,
        string subject,
        string introText,
        CancellationToken cancellationToken = default)
    {
        var pinSection = BuildPinSection(qrPin);

        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #28a745; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .footer {{ padding: 20px; text-align: center; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>CertifiedIQ</h1>
        </div>
        <div class='content'>
            <p>Dear {firstName},</p>
            <p>{introText}</p>
            {pinSection}
        </div>
        <div class='footer'>
            <p>Thank you,<br>The CertifiedIQ Team</p>
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(email, subject, body, cancellationToken);
    }

    private static string BuildPinSection(string rawPin)
    {
        // Format as "XXX XXX" for readability
        var formatted = rawPin.Length == 6
            ? $"{rawPin[..3]} {rawPin[3..]}"
            : rawPin;

        return $@"
            <div style='margin-top: 30px; border: 2px solid #28a745; border-radius: 8px; padding: 20px; background-color: #f0fff4;'>
                <h2 style='margin: 0 0 12px 0; color: #155724; font-size: 16px; text-transform: uppercase; letter-spacing: 1px;'>
                    Your Workstation Access PIN
                </h2>
                <p style='text-align: center; margin: 16px 0;'>
                    <span style='font-family: Courier New, monospace; font-size: 40px; font-weight: bold; letter-spacing: 8px; color: #155724;'>{formatted}</span>
                </p>
                <p style='margin: 12px 0 8px 0; font-size: 14px; color: #333;'>
                    This PIN identifies you at QR-enabled training stations and worksite locations.
                    When you scan a QR code at a workstation or site entrance, enter this PIN to access your training.
                </p>
                <p style='margin: 8px 0 0 0; font-size: 13px; color: #555;'>
                    <strong>Keep it safe — do not share it.</strong>
                    Training completions are recorded against your name.
                </p>
            </div>";
    }

    public async Task SendUserCreatedEmailAsync(
        string email,
        string firstName,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://quantumbuild-lms-web-production.up.railway.app";
        var loginUrl = $"{baseUrl}/login";

        var subject = "Your CertifiedIQ Account Has Been Created";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #28a745; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .button {{ display: inline-block; background-color: #28a745; color: white; padding: 12px 25px; text-decoration: none; border-radius: 5px; margin-top: 15px; }}
        .footer {{ padding: 20px; text-align: center; color: #666; font-size: 12px; }}
        .info {{ background-color: #e8f5e9; border: 1px solid #a5d6a7; padding: 15px; border-radius: 5px; margin-top: 15px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Welcome to CertifiedIQ</h1>
        </div>
        <div class='content'>
            <p>Dear {firstName},</p>
            <p>Your account on CertifiedIQ has been created. You can log in using the details below:</p>
            <div class='info'>
                <p><strong>Login URL:</strong> <a href='{loginUrl}'>{loginUrl}</a></p>
                <p><strong>Email address:</strong> {email}</p>
            </div>
            <p>Contact your administrator for your initial password.</p>
            <p style='text-align: center; margin-top: 20px;'>
                <a href='{loginUrl}' class='button'>Go to Login</a>
            </p>
        </div>
        <div class='footer'>
            <p>Thank you,<br>The CertifiedIQ Team</p>
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(email, subject, body, cancellationToken);
    }

    public async Task SendExternalReviewInvitationEmailAsync(
        string reviewerEmail,
        string talkTitle,
        string languageName,
        DateTime expiresAt,
        string portalUrl,
        string requesterName,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Translation review request — {talkTitle}";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #28a745; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .button {{ display: inline-block; background-color: #28a745; color: white; padding: 12px 25px; text-decoration: none; border-radius: 5px; margin-top: 15px; }}
        .footer {{ padding: 20px; text-align: center; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Translation Review Request</h1>
        </div>
        <div class='content'>
            <p>Hi,</p>
            <p>{requesterName} has invited you to review the translation of <strong>{talkTitle}</strong> into {languageName}.</p>
            <p style='text-align: center;'>
                <a href='{portalUrl}' class='button'>Open review</a>
            </p>
            <p style='margin-top: 20px;'>This link expires on {expiresAt:dd MMM yyyy}.</p>
        </div>
        <div class='footer'>
            <p>Thank you,<br>The CertifiedIQ Team</p>
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(reviewerEmail, subject, body, cancellationToken);
    }

    public async Task SendEmailAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        // Check if email provider is configured
        if (!_emailProvider.IsConfigured)
        {
            _logger.LogWarning(
                "Email provider not configured. Email NOT sent - To: {To}, Subject: {Subject}",
                to, subject);
            return;
        }

        var message = new EmailMessage
        {
            ToEmail = to,
            Subject = subject,
            HtmlBody = htmlBody
        };

        var result = await _emailProvider.SendAsync(message, cancellationToken);

        if (result.Success)
        {
            _logger.LogInformation(
                "Email sent successfully via IEmailProvider - To: {To}, Subject: {Subject}, MessageId: {MessageId}",
                to, subject, result.MessageId);
        }
        else
        {
            _logger.LogWarning(
                "Failed to send email via IEmailProvider - To: {To}, Subject: {Subject}, Error: {Error}",
                to, subject, result.ErrorMessage);
        }
    }
}
