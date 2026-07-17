namespace Callora.Core.Application.Mail.Contracts;

/// <summary>
/// Sends one e-mail. The host implementation is SMTP-backed and configured
/// via system config; plugins enqueue mail through the "mail.send" job or
/// call this directly for synchronous needs.
/// </summary>
public interface IMailSender
{
    /// <summary>Sends the message through the configured SMTP transport.</summary>
    Task SendAsync(MailMessage message, CancellationToken cancellationToken = default);
}
