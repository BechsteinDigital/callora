using Callora.Host.PluginContracts.Application.Mail;

namespace Callora.Core.Application.Mail;

/// <summary>
/// Job payload for one durable mail delivery ("mail.send").
/// </summary>
public sealed record MailJobPayload(MailMessage Message);
