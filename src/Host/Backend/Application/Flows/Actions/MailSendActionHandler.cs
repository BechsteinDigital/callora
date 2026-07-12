using System.Text.Json;
using Callora.Host.Backend.Application.Mail;
using Callora.Host.PluginContracts.Application.Flows;
using Callora.Host.PluginContracts.Application.Jobs;
using Callora.Host.PluginContracts.Application.Mail;

namespace Callora.Host.Backend.Application.Flows.Actions;

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
