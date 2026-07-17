namespace Callora.Core.Application.Mail.Contracts;

/// <summary>
/// One outbound e-mail. Body is plain text plus optional HTML alternative.
/// </summary>
/// <param name="To">Recipient address.</param>
/// <param name="Subject">Subject line.</param>
/// <param name="TextBody">Plain-text body.</param>
/// <param name="HtmlBody">Optional HTML alternative body.</param>
/// <param name="From">Optional sender override; falls back to the configured default.</param>
public sealed record MailMessage(
    string To,
    string Subject,
    string TextBody,
    string? HtmlBody = null,
    string? From = null);
