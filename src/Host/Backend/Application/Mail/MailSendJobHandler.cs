using System.Text.Json;
using Callora.Host.PluginContracts.Application.Jobs;
using Callora.Host.PluginContracts.Application.Mail;

namespace Callora.Host.Backend.Application.Mail;

/// <summary>
/// Durable mail delivery: failures throw so the queue retries with backoff.
/// </summary>
public sealed class MailSendJobHandler(IMailSender mailSender) : IBackgroundJobHandler
{
    public const string JobTypeName = "mail.send";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string JobType => JobTypeName;

    public async Task ExecuteAsync(BackgroundJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Deserialize<MailJobPayload>(context.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("Mail job payload could not be parsed.");

        await mailSender.SendAsync(payload.Message, cancellationToken).ConfigureAwait(false);
    }
}
