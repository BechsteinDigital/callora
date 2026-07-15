using Callora.Core.Application.Mail.Contracts;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Mail sender fake recording sent messages.
/// </summary>
public sealed class RecordingMailSender : IMailSender
{
    private readonly List<MailMessage> _sent = [];

    public IReadOnlyList<MailMessage> Sent => _sent;

    public Task SendAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        _sent.Add(message);
        return Task.CompletedTask;
    }
}
