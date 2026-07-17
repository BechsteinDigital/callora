using Callora.Core.Application.Webhooks;
using Callora.Core.Infrastructure.Webhooks;
using System.Text.Json;

namespace Callora.Core.Tests.Application.Webhooks;

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

        var minimized = WebhookPayloadMinimizer.Minimize(body, new SensitivePayloadFieldRegistry().EffectiveFields());
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
        var minimized = WebhookPayloadMinimizer.Minimize("""{ "target": "12345" }""", new SensitivePayloadFieldRegistry().EffectiveFields());
        using var document = JsonDocument.Parse(minimized);

        Assert.Equal("***", document.RootElement.GetProperty("target").GetString());
    }

    [Fact]
    public void Minimize_InvalidJson_ReturnsInputUnchanged()
    {
        Assert.Equal("not-json", WebhookPayloadMinimizer.Minimize("not-json", new SensitivePayloadFieldRegistry().EffectiveFields()));
    }

    [Fact]
    public void CoreBaseline_DoesNotMaskPluginDomainFields()
    {
        // Domain-neutral: caller/callee numbers are no longer a core field.
        var minimized = WebhookPayloadMinimizer.Minimize(
            """{ "callerNumber": "+4917612345678" }""",
            new SensitivePayloadFieldRegistry().EffectiveFields());
        using var document = JsonDocument.Parse(minimized);

        Assert.Equal("+4917612345678", document.RootElement.GetProperty("callerNumber").GetString());
    }

    [Fact]
    public void Registry_MasksPluginDeclaredFields_OnTopOfCoreBaseline()
    {
        var registry = new SensitivePayloadFieldRegistry();
        registry.RegisterPluginFields("communication", ["callerNumber", "calleeNumber"]);

        var minimized = WebhookPayloadMinimizer.Minimize(
            """{ "callerNumber": "+4917612345678", "email": "max@example.org", "note": "keep" }""",
            registry.EffectiveFields());
        using var document = JsonDocument.Parse(minimized);

        Assert.Equal("+49***78", document.RootElement.GetProperty("callerNumber").GetString());
        Assert.Equal("max***rg", document.RootElement.GetProperty("email").GetString());
        Assert.Equal("keep", document.RootElement.GetProperty("note").GetString());
    }

    [Fact]
    public void Registry_ClearRemovesPluginFields()
    {
        var registry = new SensitivePayloadFieldRegistry();
        registry.RegisterPluginFields("communication", ["callerNumber"]);
        registry.ClearPluginFields("communication");

        Assert.DoesNotContain("callerNumber", registry.EffectiveFields());
        Assert.Contains("email", registry.EffectiveFields());
    }

    [Fact]
    public void ParseSensitiveFields_ReadsDeclaredArray()
    {
        var fields = RegistrySensitiveFieldSyncService.ParseSensitiveFields(
            """{ "sensitiveFields": ["phoneNumber", "callerNumber"] }""");

        Assert.Equal(["phoneNumber", "callerNumber"], fields);
    }
}
