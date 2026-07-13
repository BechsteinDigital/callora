using System.Text.Json;
using Callora.Host.Backend.Application.Webhooks;

namespace Callora.Host.Backend.Tests.Application.Webhooks;

public sealed class WebhookPayloadMinimizerTests
{
    [Fact]
    public void Minimize_MasksSensitiveFieldsRecursively_AndKeepsOtherValues()
    {
        var body = """
        {
          "event": "call.ringing",
          "workspaceKey": "workspace-a",
          "data": {
            "callId": "c1",
            "targetValue": "+4917674717849",
            "targetDisplayName": "Max Mustermann",
            "state": "Ringing",
            "nested": { "email": "max@example.org" }
          }
        }
        """;

        var minimized = WebhookPayloadMinimizer.Minimize(body);
        using var document = JsonDocument.Parse(minimized);
        var data = document.RootElement.GetProperty("data");

        Assert.Equal("+49***49", data.GetProperty("targetValue").GetString());
        Assert.Equal("Max***nn", data.GetProperty("targetDisplayName").GetString());
        Assert.Equal("max***rg", data.GetProperty("nested").GetProperty("email").GetString());
        Assert.Equal("c1", data.GetProperty("callId").GetString());
        Assert.Equal("Ringing", data.GetProperty("state").GetString());
        Assert.Equal("workspace-a", document.RootElement.GetProperty("workspaceKey").GetString());
    }

    [Fact]
    public void Minimize_ShortValuesBecomeFullyMasked()
    {
        var minimized = WebhookPayloadMinimizer.Minimize("""{ "target": "12345" }""");
        using var document = JsonDocument.Parse(minimized);

        Assert.Equal("***", document.RootElement.GetProperty("target").GetString());
    }

    [Fact]
    public void Minimize_InvalidJson_ReturnsInputUnchanged()
    {
        Assert.Equal("not-json", WebhookPayloadMinimizer.Minimize("not-json"));
    }
}
