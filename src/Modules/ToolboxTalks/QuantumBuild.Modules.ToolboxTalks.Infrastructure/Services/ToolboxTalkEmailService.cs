using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Abstractions.Email;
using QuantumBuild.Core.Application.Features.TenantSettings;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services;

/// <summary>
/// Email service for Toolbox Talk notifications.
/// Uses IEmailProvider for actual email delivery (SendGrid, MailerSend, SMTP, etc.)
/// </summary>
public class ToolboxTalkEmailService : IToolboxTalkEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ToolboxTalkEmailService> _logger;
    private readonly IEmailProvider _emailProvider;
    private readonly ITenantSettingsService _tenantSettingsService;

    public ToolboxTalkEmailService(
        IConfiguration configuration,
        ILogger<ToolboxTalkEmailService> logger,
        IEmailProvider emailProvider,
        ITenantSettingsService tenantSettingsService)
    {
        _configuration = configuration;
        _logger = logger;
        _emailProvider = emailProvider;
        _tenantSettingsService = tenantSettingsService;
    }

    private async Task<string> GetTeamNameAsync(Guid tenantId, CancellationToken ct)
    {
        return await _tenantSettingsService.GetSettingAsync(
            tenantId, TenantSettingKeys.EmailTeamName, TenantSettingKeys.Defaults.EmailTeamName, ct);
    }

    public async Task SendTalkAssignmentEmailAsync(
        ScheduledTalk scheduledTalk,
        Employee employee,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(employee.Email))
        {
            _logger.LogWarning(
                "Cannot send assignment email: Employee {EmployeeId} has no email address",
                employee.Id);
            return;
        }

        var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://quantumbuild-lms-web-production.up.railway.app";
        var talkUrl = $"{baseUrl}/login?returnUrl={Uri.EscapeDataString($"/toolbox-talks/{scheduledTalk.Id}")}";
        var teamName = await GetTeamNameAsync(scheduledTalk.TenantId, cancellationToken);

        var subject = $"New Learning Assigned: {scheduledTalk.ToolboxTalk.Title}";
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
            <h1>New Learning Assigned</h1>
        </div>
        <div class='content'>
            <p>Dear {employee.FirstName},</p>
            <p>A new learning has been assigned to you:</p>
            <p><strong>{scheduledTalk.ToolboxTalk.Title}</strong></p>
            {(string.IsNullOrEmpty(scheduledTalk.ToolboxTalk.Description) ? "" : $"<p>{scheduledTalk.ToolboxTalk.Description}</p>")}
            <p><strong>Due Date:</strong> {scheduledTalk.DueDate:dd MMM yyyy}</p>
            <p>
                <a href='{talkUrl}' class='button'>Start Talk</a>
            </p>
        </div>
        <div class='footer'>
            <p>Thank you,<br>{teamName}</p>
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

        var emailMessage = new EmailMessage
        {
            ToEmail = employee.Email,
            ToName = $"{employee.FirstName} {employee.LastName}",
            Subject = subject,
            HtmlBody = body
        };

        var result = await _emailProvider.SendAsync(emailMessage, cancellationToken);

        if (result.Success)
        {
            _logger.LogInformation(
                "Toolbox Talk assignment email sent to {Email} for talk {TalkId}",
                employee.Email, scheduledTalk.Id);
        }
        else
        {
            _logger.LogWarning(
                "Failed to send Toolbox Talk assignment email to {Email}: {Error}",
                employee.Email, result.ErrorMessage);
        }
    }

    public async Task SendReminderEmailAsync(
        ScheduledTalk scheduledTalk,
        Employee employee,
        int reminderNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(employee.Email))
        {
            _logger.LogWarning(
                "Cannot send reminder email: Employee {EmployeeId} has no email address",
                employee.Id);
            return;
        }

        var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://quantumbuild-lms-web-production.up.railway.app";
        var talkUrl = $"{baseUrl}/login?returnUrl={Uri.EscapeDataString($"/toolbox-talks/{scheduledTalk.Id}")}";
        var teamName = await GetTeamNameAsync(scheduledTalk.TenantId, cancellationToken);

        var urgency = reminderNumber >= 3 ? "URGENT: " : "";
        var subject = $"{urgency}Reminder {reminderNumber}: {scheduledTalk.ToolboxTalk.Title}";

        var daysOverdue = (DateTime.Today - scheduledTalk.DueDate.Date).Days;
        var overdueText = daysOverdue > 0
            ? $"<p style='color: #dc3545; font-weight: bold;'>This talk is {daysOverdue} day{(daysOverdue == 1 ? "" : "s")} overdue.</p>"
            : $"<p style='color: #ffc107; font-weight: bold;'>This talk is due today.</p>";

        var buttonColor = reminderNumber >= 3 ? "#dc3545" : "#ffc107";

        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: {buttonColor}; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .button {{ display: inline-block; background-color: {buttonColor}; color: white; padding: 12px 25px; text-decoration: none; border-radius: 5px; margin-top: 15px; }}
        .footer {{ padding: 20px; text-align: center; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Reminder #{reminderNumber}</h1>
        </div>
        <div class='content'>
            <p>Dear {employee.FirstName},</p>
            <p>This is reminder #{reminderNumber} for an overdue learning:</p>
            <p><strong>{scheduledTalk.ToolboxTalk.Title}</strong></p>
            {overdueText}
            <p><strong>Original Due Date:</strong> {scheduledTalk.DueDate:dd MMM yyyy}</p>
            <p>
                <a href='{talkUrl}' class='button'>Complete Now</a>
            </p>
        </div>
        <div class='footer'>
            <p>Thank you,<br>{teamName}</p>
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

        var emailMessage = new EmailMessage
        {
            ToEmail = employee.Email,
            ToName = $"{employee.FirstName} {employee.LastName}",
            Subject = subject,
            HtmlBody = body
        };

        var result = await _emailProvider.SendAsync(emailMessage, cancellationToken);

        if (result.Success)
        {
            _logger.LogInformation(
                "Toolbox Talk reminder email sent to {Email} for talk {TalkId}, reminder #{ReminderNumber}",
                employee.Email, scheduledTalk.Id, reminderNumber);
        }
        else
        {
            _logger.LogWarning(
                "Failed to send Toolbox Talk reminder email to {Email}: {Error}",
                employee.Email, result.ErrorMessage);
        }
    }

    public async Task SendCompletionConfirmationEmailAsync(
        ScheduledTalkCompletion completion,
        Employee employee,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(employee.Email))
        {
            _logger.LogWarning(
                "Cannot send completion email: Employee {EmployeeId} has no email address",
                employee.Id);
            return;
        }

        var scheduledTalk = completion.ScheduledTalk;
        var teamName = await GetTeamNameAsync(scheduledTalk.TenantId, cancellationToken);
        var subject = $"Learning Completed: {scheduledTalk.ToolboxTalk.Title}";

        var quizSection = "";
        if (completion.QuizScore.HasValue && completion.QuizMaxScore.HasValue)
        {
            var passStatus = completion.QuizPassed == true ? "Passed" : "Failed";
            var passColor = completion.QuizPassed == true ? "#28a745" : "#dc3545";
            quizSection = $@"
            <p><strong>Quiz Results:</strong></p>
            <p>Score: {completion.QuizScore}/{completion.QuizMaxScore} (<span style='color: {passColor};'>{passStatus}</span>)</p>";
        }

        var certificateSection = "";
        if (!string.IsNullOrEmpty(completion.CertificateUrl))
        {
            certificateSection = $@"
            <p><a href='{completion.CertificateUrl}' style='color: #007bff;'>Download your completion certificate</a></p>";
        }

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
        .success-icon {{ font-size: 48px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <div class='success-icon'>&#10004;</div>
            <h1>Learning Completed</h1>
        </div>
        <div class='content'>
            <p>Dear {employee.FirstName},</p>
            <p>Thank you for completing the learning:</p>
            <p><strong>{scheduledTalk.ToolboxTalk.Title}</strong></p>
            <p><strong>Completed:</strong> {completion.CompletedAt:dd MMM yyyy HH:mm}</p>
            <p><strong>Time Spent:</strong> {FormatTimeSpent(completion.TotalTimeSpentSeconds)}</p>
            {quizSection}
            {certificateSection}
            <p>Your completion has been recorded.</p>
        </div>
        <div class='footer'>
            <p>Thank you,<br>{teamName}</p>
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

        var emailMessage = new EmailMessage
        {
            ToEmail = employee.Email,
            ToName = $"{employee.FirstName} {employee.LastName}",
            Subject = subject,
            HtmlBody = body
        };

        var result = await _emailProvider.SendAsync(emailMessage, cancellationToken);

        if (result.Success)
        {
            _logger.LogInformation(
                "Toolbox Talk completion email sent to {Email} for completion {CompletionId}",
                employee.Email, completion.Id);
        }
        else
        {
            _logger.LogWarning(
                "Failed to send Toolbox Talk completion email to {Email}: {Error}",
                employee.Email, result.ErrorMessage);
        }
    }

    public async Task SendEscalationEmailAsync(
        ScheduledTalk scheduledTalk,
        Employee employee,
        Employee manager,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(manager.Email))
        {
            _logger.LogWarning(
                "Cannot send escalation email: Manager {ManagerId} has no email address",
                manager.Id);
            return;
        }

        var daysOverdue = (DateTime.Today - scheduledTalk.DueDate.Date).Days;
        var teamName = await GetTeamNameAsync(scheduledTalk.TenantId, cancellationToken);
        var subject = $"Escalation: Overdue Learning - {employee.FullName}";

        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #dc3545; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .footer {{ padding: 20px; text-align: center; color: #666; font-size: 12px; }}
        .details-table {{ width: 100%; border-collapse: collapse; margin: 15px 0; }}
        .details-table td {{ padding: 8px; border-bottom: 1px solid #ddd; }}
        .details-table td:first-child {{ font-weight: bold; width: 40%; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Escalation Notice</h1>
        </div>
        <div class='content'>
            <p>Dear {manager.FirstName},</p>
            <p>The following learning remains incomplete after multiple reminders and requires your attention:</p>
            <table class='details-table'>
                <tr>
                    <td>Employee:</td>
                    <td>{employee.FullName}</td>
                </tr>
                <tr>
                    <td>Talk:</td>
                    <td>{scheduledTalk.ToolboxTalk.Title}</td>
                </tr>
                <tr>
                    <td>Original Due Date:</td>
                    <td>{scheduledTalk.DueDate:dd MMM yyyy}</td>
                </tr>
                <tr>
                    <td>Days Overdue:</td>
                    <td style='color: #dc3545; font-weight: bold;'>{daysOverdue}</td>
                </tr>
                <tr>
                    <td>Reminders Sent:</td>
                    <td>{scheduledTalk.RemindersSent}</td>
                </tr>
            </table>
            <p>Please follow up with {employee.FirstName} to ensure compliance with safety training requirements.</p>
        </div>
        <div class='footer'>
            <p>Thank you,<br>{teamName}</p>
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

        // Send to manager
        var managerEmail = new EmailMessage
        {
            ToEmail = manager.Email,
            ToName = $"{manager.FirstName} {manager.LastName}",
            Subject = subject,
            HtmlBody = body
        };

        var result = await _emailProvider.SendAsync(managerEmail, cancellationToken);

        if (result.Success)
        {
            _logger.LogInformation(
                "Toolbox Talk escalation email sent to manager {Email} for talk {TalkId}",
                manager.Email, scheduledTalk.Id);
        }
        else
        {
            _logger.LogWarning(
                "Failed to send Toolbox Talk escalation email to manager {Email}: {Error}",
                manager.Email, result.ErrorMessage);
        }

        // Also send to the employee if they have an email
        if (!string.IsNullOrEmpty(employee.Email))
        {
            var employeeEmail = new EmailMessage
            {
                ToEmail = employee.Email,
                ToName = $"{employee.FirstName} {employee.LastName}",
                Subject = subject,
                HtmlBody = body
            };

            var employeeResult = await _emailProvider.SendAsync(employeeEmail, cancellationToken);

            if (employeeResult.Success)
            {
                _logger.LogInformation(
                    "Toolbox Talk escalation copy sent to employee {Email} for talk {TalkId}",
                    employee.Email, scheduledTalk.Id);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to send Toolbox Talk escalation copy to employee {Email}: {Error}",
                    employee.Email, employeeResult.ErrorMessage);
            }
        }
    }

    public async Task SendRefresherReminderAsync(
        ScheduledTalk refresherTalk,
        Employee employee,
        string timeframe,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(employee?.Email))
        {
            _logger.LogWarning("Cannot send refresher reminder: Employee {EmployeeId} has no email", employee?.Id);
            return;
        }

        var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://quantumbuild-lms-web-production.up.railway.app";
        var talkUrl = $"{baseUrl}/login?returnUrl={Uri.EscapeDataString($"/toolbox-talks/{refresherTalk.Id}")}";
        var teamName = await GetTeamNameAsync(refresherTalk.TenantId, cancellationToken);

        var subject = $"Refresher Training Due in {timeframe}: {refresherTalk.ToolboxTalk.Title}";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #f97316; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .button {{ display: inline-block; background-color: #f97316; color: white; padding: 12px 25px; text-decoration: none; border-radius: 5px; margin-top: 15px; }}
        .footer {{ padding: 20px; text-align: center; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Refresher Training Due</h1>
        </div>
        <div class='content'>
            <p>Dear {employee.FirstName},</p>
            <p>Your refresher training for <strong>{refresherTalk.ToolboxTalk.Title}</strong> is due in {timeframe}.</p>
            <p><strong>Due Date:</strong> {refresherTalk.RefresherDueDate:dd MMM yyyy}</p>
            <p>
                <a href='{talkUrl}' class='button'>Complete Training</a>
            </p>
        </div>
        <div class='footer'>
            <p>Thank you,<br>{teamName}</p>
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

        var emailMessage = new EmailMessage
        {
            ToEmail = employee.Email,
            ToName = $"{employee.FirstName} {employee.LastName}",
            Subject = subject,
            HtmlBody = body
        };

        var result = await _emailProvider.SendAsync(emailMessage, cancellationToken);

        if (result.Success)
        {
            _logger.LogInformation("Refresher reminder sent to {Email} for talk {TalkId}, timeframe: {Timeframe}",
                employee.Email, refresherTalk.Id, timeframe);
        }
        else
        {
            _logger.LogWarning("Failed to send refresher reminder to {Email}: {Error}",
                employee.Email, result.ErrorMessage);
        }
    }

    public async Task SendCourseAssignmentEmailAsync(
        ToolboxTalkCourse course,
        Employee employee,
        int talkCount,
        DateTime? dueDate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(employee.Email))
        {
            _logger.LogWarning(
                "Cannot send course assignment email: Employee {EmployeeId} has no email address",
                employee.Id);
            return;
        }

        var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://quantumbuild-lms-web-production.up.railway.app";
        var courseUrl = $"{baseUrl}/login?returnUrl={Uri.EscapeDataString("/toolbox-talks")}";
        var teamName = await GetTeamNameAsync(course.TenantId, cancellationToken);

        var subject = $"New Training Course Assigned: {course.Title}";
        var dueDateHtml = dueDate.HasValue
            ? $"<p><strong>Due Date:</strong> {dueDate.Value:dd MMM yyyy}</p>"
            : "";

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
            <h1>New Training Course Assigned</h1>
        </div>
        <div class='content'>
            <p>Dear {employee.FirstName},</p>
            <p>You have been assigned a new training course:</p>
            <p><strong>{course.Title}</strong></p>
            {(string.IsNullOrEmpty(course.Description) ? "" : $"<p>{course.Description}</p>")}
            <p>This course contains <strong>{talkCount}</strong> training item{(talkCount == 1 ? "" : "s")}.</p>
            {dueDateHtml}
            <p>Please complete all items in the course by the due date.</p>
            <p>
                <a href='{courseUrl}' class='button'>View Course</a>
            </p>
        </div>
        <div class='footer'>
            <p>Thank you,<br>{teamName}</p>
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

        var emailMessage = new EmailMessage
        {
            ToEmail = employee.Email,
            ToName = $"{employee.FirstName} {employee.LastName}",
            Subject = subject,
            HtmlBody = body
        };

        var result = await _emailProvider.SendAsync(emailMessage, cancellationToken);

        if (result.Success)
        {
            _logger.LogInformation(
                "Course assignment email sent to {Email} for course {CourseId} ({CourseTitle})",
                employee.Email, course.Id, course.Title);
        }
        else
        {
            _logger.LogWarning(
                "Failed to send course assignment email to {Email}: {Error}",
                employee.Email, result.ErrorMessage);
        }
    }

    public async Task SendCourseRefresherReminderAsync(
        ToolboxTalkCourseAssignment refresherAssignment,
        Employee employee,
        string timeframe,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(employee?.Email))
        {
            _logger.LogWarning("Cannot send course refresher reminder: Employee {EmployeeId} has no email", employee?.Id);
            return;
        }

        var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://quantumbuild-lms-web-production.up.railway.app";
        var courseUrl = $"{baseUrl}/login?returnUrl={Uri.EscapeDataString($"/toolbox-talks/courses/{refresherAssignment.Id}")}";
        var teamName = await GetTeamNameAsync(refresherAssignment.TenantId, cancellationToken);

        var subject = $"Course Refresher Due in {timeframe}: {refresherAssignment.Course.Title}";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #f97316; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .button {{ display: inline-block; background-color: #f97316; color: white; padding: 12px 25px; text-decoration: none; border-radius: 5px; margin-top: 15px; }}
        .footer {{ padding: 20px; text-align: center; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Course Refresher Due</h1>
        </div>
        <div class='content'>
            <p>Dear {employee.FirstName},</p>
            <p>Your refresher course <strong>{refresherAssignment.Course.Title}</strong> is due in {timeframe}.</p>
            <p><strong>Due Date:</strong> {refresherAssignment.RefresherDueDate:dd MMM yyyy}</p>
            <p>
                <a href='{courseUrl}' class='button'>Start Course</a>
            </p>
        </div>
        <div class='footer'>
            <p>Thank you,<br>{teamName}</p>
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

        var emailMessage = new EmailMessage
        {
            ToEmail = employee.Email,
            ToName = $"{employee.FirstName} {employee.LastName}",
            Subject = subject,
            HtmlBody = body
        };

        var result = await _emailProvider.SendAsync(emailMessage, cancellationToken);

        if (result.Success)
        {
            _logger.LogInformation("Course refresher reminder sent to {Email} for assignment {AssignmentId}, timeframe: {Timeframe}",
                employee.Email, refresherAssignment.Id, timeframe);
        }
        else
        {
            _logger.LogWarning("Failed to send course refresher reminder to {Email}: {Error}",
                employee.Email, result.ErrorMessage);
        }
    }

    private static string FormatTimeSpent(int totalSeconds)
    {
        if (totalSeconds < 60)
            return $"{totalSeconds} seconds";

        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;

        if (minutes < 60)
            return seconds > 0 ? $"{minutes} min {seconds} sec" : $"{minutes} min";

        var hours = minutes / 60;
        minutes %= 60;

        return minutes > 0 ? $"{hours} hr {minutes} min" : $"{hours} hr";
    }
}
