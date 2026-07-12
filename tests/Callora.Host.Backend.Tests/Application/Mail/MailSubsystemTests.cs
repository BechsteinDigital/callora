using System.Text.Json;
using Callora.Host.Backend.Application.Mail;
using Callora.Host.Backend.Tests.Support;
using Callora.Host.PluginContracts.Application.Jobs;
using Callora.Host.PluginContracts.Application.Mail;
using Xunit;

namespace Callora.Host.Backend.Tests.Application.Mail;

public sealed class MailSubsystemTests
{
    [Fact]
    public void TemplateRenderer_ReplacesKnownTokens_KeepsUnknownLiteral()
    {
        var rendered = MailTemplateRenderer.Render(
            "Hallo {{name}}, dein Workspace ist {{workspace}}. {{unknown}}",
            new Dictionary<string, string> { ["name"] = "Alex", ["workspace"] = "test" });

        Assert.Equal("Hallo Alex, dein Workspace ist test. {{unknown}}", rendered);
    }

    [Fact]
    public async Task MailJobHandler_SendsParsedMessage()
    {
        var sender = new RecordingMailSender();
        var handler = new MailSendJobHandler(sender);
        var payload = JsonSerializer.Serialize(
            new MailJobPayload(new MailMessage("user@example.org", "Willkommen", "Hallo!")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        await handler.ExecuteAsync(new BackgroundJobExecutionContext(
            Guid.NewGuid(), MailSendJobHandler.JobTypeName, payload, null, Attempt: 1));

        var sent = Assert.Single(sender.Sent);
        Assert.Equal("user@example.org", sent.To);
        Assert.Equal("Willkommen", sent.Subject);
    }
}
