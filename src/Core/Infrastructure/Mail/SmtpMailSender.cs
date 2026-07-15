using System.Net;
using Callora.Core.Application.Configuration.Contracts;
using Callora.Core.Application.Mail.Contracts;

namespace Callora.Core.Infrastructure.Mail;

/// <summary>
/// SMTP delivery configured via system config (plugin "host", keys mail.smtp.*).
/// Without a configured host, mails are logged instead of sent so development
/// environments work without an SMTP server.
/// </summary>
public sealed class SmtpMailSender(
    IPluginConfigReader configReader,
    ILogger<SmtpMailSender> logger) : IMailSender
{
    public const string ConfigPluginId = "host";

    public async Task SendAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var host = await configReader.GetStringAsync(ConfigPluginId, "mail.smtp.host", cancellationToken: cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogInformation(
                "No SMTP host configured (host/mail.smtp.host); mail to {Recipient} with subject '{Subject}' was not sent.",
                MaskRecipient(message.To),
                message.Subject);
            return;
        }

        var port = await configReader.GetIntAsync(ConfigPluginId, "mail.smtp.port", fallback: 587, cancellationToken: cancellationToken).ConfigureAwait(false);
        var user = await configReader.GetStringAsync(ConfigPluginId, "mail.smtp.username", cancellationToken: cancellationToken).ConfigureAwait(false);
        var password = await configReader.GetStringAsync(ConfigPluginId, "mail.smtp.password", cancellationToken: cancellationToken).ConfigureAwait(false);
        var from = message.From
            ?? await configReader.GetStringAsync(ConfigPluginId, "mail.smtp.from", cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? "noreply@callora.local";
        var useSsl = await configReader.GetBoolAsync(ConfigPluginId, "mail.smtp.ssl", fallback: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        using var client = new System.Net.Mail.SmtpClient(host, port)
        {
            EnableSsl = useSsl,
            Credentials = string.IsNullOrWhiteSpace(user)
                ? null
                : new NetworkCredential(user, password)
        };

        using var mail = new System.Net.Mail.MailMessage(from, message.To, message.Subject, message.TextBody);
        if (!string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            mail.AlternateViews.Add(System.Net.Mail.AlternateView.CreateAlternateViewFromString(
                message.HtmlBody, null, "text/html"));
        }

        await client.SendMailAsync(mail, cancellationToken).ConfigureAwait(false);
    }

    // DSGVO: Empfängeradressen erreichen die Logs nie unmaskiert.
    private static string MaskRecipient(string recipient)
    {
        var atIndex = recipient.IndexOf('@');
        return atIndex > 1
            ? $"{recipient[..2]}***{recipient[atIndex..]}"
            : "***";
    }
}
