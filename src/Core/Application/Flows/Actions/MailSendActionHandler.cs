using System.Text.Json;
using Callora.Core.Application.Mail;
using Callora.Core.Application.Flows.Contracts;
using Callora.Core.Application.Jobs.Contracts;
using Callora.Core.Application.Mail.Contracts;

namespace Callora.Core.Application.Flows.Actions;

/// <summary>
/// Enqueues a durable mail ("to", "subject", "body"); body placeholders like
/// {{target}} resolve from the event data.
/// </summary>
public sealed class MailSendActionHandler(IBackgroundJobQueue jobQueue) : IFlowActionHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Type => "mail.send";

    public async Task ExecuteAsync(
        RuleContext context,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        if (!parameters.TryGetValue("to", out var to) || string.IsNullOrWhiteSpace(to))
        {
            throw new InvalidOperationException("mail.send requires a 'to' parameter.");
        }

        var model = new Dictionary<string, string>(context.Data, StringComparer.OrdinalIgnoreCase)
        {
            ["event"] = context.EventName,
            ["workspace"] = context.WorkspaceKey ?? string.Empty
        };
        var message = new MailMessage(
            to,
            MailTemplateRenderer.Render(parameters.GetValueOrDefault("subject", "Callora: {{event}}"), model),
            MailTemplateRenderer.Render(parameters.GetValueOrDefault("body", string.Empty), model));

        await jobQueue.EnqueueAsync(
                new BackgroundJobRequest(
                    MailSendJobHandler.JobTypeName,
                    JsonSerializer.Serialize(new MailJobPayload(message), JsonOptions),
                    MaxAttempts: 5,
                    WorkspaceKey: context.WorkspaceKey),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
