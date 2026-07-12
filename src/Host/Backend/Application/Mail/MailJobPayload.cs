using Callora.Host.PluginContracts.Application.Mail;

namespace Callora.Host.Backend.Application.Mail;

/// <summary>
/// Job payload for one durable mail delivery ("mail.send").
/// </summary>
public sealed record MailJobPayload(MailMessage Message);
