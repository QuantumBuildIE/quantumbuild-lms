using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Abstractions.Email;

namespace QuantumBuild.Core.Infrastructure.Services.Email;

public class StubEmailProvider : IEmailProvider
{
    private readonly ILogger<StubEmailProvider> _logger;

    public StubEmailProvider(ILogger<StubEmailProvider> logger)
    {
        _logger = logger;
    }

    public bool IsConfigured => true;

    public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[StubEmailProvider] Email logged (not sent) — To: {To}, Subject: {Subject}, BodyLength: {Length}",
            message.ToEmail, message.Subject, message.HtmlBody?.Length ?? 0);

        return Task.FromResult(EmailSendResult.Succeeded("stub-" + Guid.NewGuid().ToString("N")[..8]));
    }
}
