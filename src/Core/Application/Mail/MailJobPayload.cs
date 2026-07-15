using Callora.Core.Application.Mail.Contracts;

namespace Callora.Core.Application.Mail;

/// <summary>
/// Job payload for one durable mail delivery ("mail.send").
/// </summary>
public sealed record MailJobPayload(MailMessage Message);
