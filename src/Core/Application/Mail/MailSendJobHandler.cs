using Callora.Core.Application.Jobs.Contracts;
using Callora.Core.Application.Mail.Contracts;
using Callora.Core.Extensibility;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Callora.Core.Application.Mail;

/// <summary>
/// Durable mail delivery: failures throw so the queue retries with backoff.
/// </summary>
[HostProtected]
public sealed class MailSendJobHandler(
    IMailSender mailSender,
    ILogger<MailSendJobHandler> logger) : IBackgroundJobHandler
{
    public const string JobTypeName = "mail.send";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string JobType => JobTypeName;

    public async Task ExecuteAsync(BackgroundJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Deserialize<MailJobPayload>(context.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("Mail job payload could not be parsed.");

        // Empfänger bleibt bewusst außerhalb des Logs (PII); die Job-Id
        // korreliert mit dem background_jobs-Eintrag.
        logger.LogInformation(
            "Sending mail for job {JobId} (attempt {Attempt}).",
            context.JobId,
            context.Attempt);
        await mailSender.SendAsync(payload.Message, cancellationToken).ConfigureAwait(false);
    }
}
