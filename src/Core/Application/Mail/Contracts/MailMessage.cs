namespace Callora.Core.Application.Mail.Contracts;

/// <summary>
/// One outbound e-mail. Body is plain text plus optional HTML alternative.
/// </summary>
public sealed record MailMessage(
    string To,
    string Subject,
    string TextBody,
    string? HtmlBody = null,
    string? From = null);
