using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Core.Application.Abstractions.Email;

namespace QuantumBuild.Core.Infrastructure.Services.Email;

public class MailerSendEmailProvider : IEmailProvider
{
    private readonly HttpClient _httpClient;
    private readonly EmailProviderSettings _settings;
    private readonly ILogger<MailerSendEmailProvider> _logger;

    public MailerSendEmailProvider(
        HttpClient httpClient,
        IOptions<EmailProviderSettings> settings,
        ILogger<MailerSendEmailProvider> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.ApiKey);

    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return EmailSendResult.NotConfigured();

        try
        {
            var payload = new MailerSendRequest
            {
                From = new MailerSendAddress { Email = _settings.FromEmail, Name = _settings.FromName },
                To = [new MailerSendAddress { Email = message.ToEmail, Name = message.ToName }],
                Subject = message.Subject,
                Html = message.HtmlBody,
                Text = message.PlainTextBody
            };

            if (!string.IsNullOrWhiteSpace(message.ReplyToEmail))
            {
                payload.ReplyTo = new MailerSendAddress
                {
                    Email = message.ReplyToEmail,
                    Name = message.ReplyToName
                };
            }

            var response = await _httpClient.PostAsJsonAsync(
                "https://api.mailersend.com/v1/email",
                payload,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var messageId = response.Headers.Contains("X-Message-Id")
                    ? response.Headers.GetValues("X-Message-Id").FirstOrDefault()
                    : null;

                _logger.LogInformation(
                    "MailerSend email sent successfully to {To}, MessageId: {MessageId}",
                    message.ToEmail, messageId);

                return EmailSendResult.Succeeded(messageId);
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "MailerSend API returned {StatusCode}: {Error}",
                (int)response.StatusCode, errorBody);

            return EmailSendResult.Failed($"MailerSend API returned {(int)response.StatusCode}: {errorBody}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via MailerSend to {To}", message.ToEmail);
            return EmailSendResult.Failed(ex.Message);
        }
    }

    private class MailerSendRequest
    {
        [JsonPropertyName("from")]
        public required MailerSendAddress From { get; set; }

        [JsonPropertyName("to")]
        public required List<MailerSendAddress> To { get; set; }

        [JsonPropertyName("subject")]
        public required string Subject { get; set; }

        [JsonPropertyName("html")]
        public required string Html { get; set; }

        [JsonPropertyName("text")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Text { get; set; }

        [JsonPropertyName("reply_to")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MailerSendAddress? ReplyTo { get; set; }
    }

    private class MailerSendAddress
    {
        [JsonPropertyName("email")]
        public required string Email { get; set; }

        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; set; }
    }
}
