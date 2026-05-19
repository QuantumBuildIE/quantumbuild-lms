namespace QuantumBuild.Core.Application.Features.BulkImport;

public class BulkImportSettings
{
    public const string SectionName = "BulkImport";

    /// <summary>
    /// Milliseconds to wait between invitation email sends during the bulk-import job.
    /// MailerSend's documented API throughput limit is 10 requests/second; 1 100 ms
    /// (≈ 0.9 req/sec) keeps the job well within that ceiling and leaves headroom for
    /// other email traffic from concurrent jobs or scheduled reminders.
    /// Increase if the provider returns 429s; decrease only after confirming the plan limit.
    /// </summary>
    public int InvitationEmailDelayMs { get; set; } = 1_100;
}
