using Callora.Core.Application.Webhooks;
using Callora.Core.Tests.Cli;
using Xunit;

namespace Callora.Core.Tests.Documentation;

/// <summary>
/// Hält die Webhook-Anleitung ehrlich über das, was Empfänger tatsächlich bekommen.
/// </summary>
/// <remarks>
/// <para>
/// Diese Seite ist kein Betriebsdokument, sondern ein Vertrag: Wer einen Empfänger baut, schreibt
/// die Header-Namen von hier ab. Ein Name, der im Code wandert und in der Anleitung stehen bleibt,
/// bricht fremden Code — und zwar erst dann, wenn jemand anderes deployt.
/// </para>
/// <para>
/// Der Deduplizierungs-Hinweis wird mitgeprüft, weil er die einzige Stelle ist, an der ein
/// Empfänger überhaupt erfährt, dass Zustellungen sich wiederholen. Ohne ihn ist der Header da und
/// niemand weiß, wofür.
/// </para>
/// </remarks>
public sealed class DocumentedWebhookDeliveryContractTests
{
    private static readonly string GuidePath = Path.Combine(
        ScaffoldedPluginFixture.ResolveRepositoryRoot(), "docs-site", "guides", "automation", "webhooks.md");

    [Fact]
    public void EveryDeliveryHeaderIsDocumented()
    {
        var guide = File.ReadAllText(GuidePath);

        Assert.Contains(WebhookSignature.EventHeaderName, guide, StringComparison.Ordinal);
        Assert.Contains(WebhookSignature.HeaderName, guide, StringComparison.Ordinal);
        Assert.Contains(WebhookSignature.DeliveryHeaderName, guide, StringComparison.Ordinal);
    }

    [Fact]
    public void TheGuideTellsReceiversToDeduplicate()
    {
        var guide = File.ReadAllText(GuidePath);

        Assert.Contains("deduplicate", guide, StringComparison.OrdinalIgnoreCase);
    }
}
