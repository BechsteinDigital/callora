using System.Text.Json;
using System.Text.Json.Nodes;

namespace Callora.Core.Application.Webhooks;

/// <summary>
/// Data minimization for outbound webhook payloads (PLAT-244): masks values of
/// the supplied sensitive field names recursively before the payload leaves the
/// platform. The field set is domain-neutral — the core carries a generic
/// baseline and plugins declare their own via <see cref="SensitivePayloadFieldRegistry"/>.
/// Subscriptions opt in to unmasked payloads explicitly.
/// </summary>
public static class WebhookPayloadMinimizer
{
    public static string Minimize(string bodyJson, IReadOnlySet<string> sensitiveFields)
    {
        ArgumentNullException.ThrowIfNull(bodyJson);
        ArgumentNullException.ThrowIfNull(sensitiveFields);

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(bodyJson);
        }
        catch (JsonException)
        {
            return bodyJson;
        }

        if (root is null)
        {
            return bodyJson;
        }

        MaskNode(root, sensitiveFields);
        return root.ToJsonString();
    }

    public static string MaskValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var trimmed = value.Trim();
        return trimmed.Length > 5
            ? $"{trimmed[..3]}***{trimmed[^2..]}"
            : "***";
    }

    private static void MaskNode(JsonNode node, IReadOnlySet<string> sensitiveFields)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var propertyName in jsonObject.Select(static property => property.Key).ToArray())
                {
                    var child = jsonObject[propertyName];
                    if (child is JsonValue value &&
                        sensitiveFields.Contains(propertyName) &&
                        value.TryGetValue<string>(out var text))
                    {
                        jsonObject[propertyName] = MaskValue(text);
                        continue;
                    }

                    if (child is not null)
                    {
                        MaskNode(child, sensitiveFields);
                    }
                }

                break;
            case JsonArray jsonArray:
                foreach (var element in jsonArray)
                {
                    if (element is not null)
                    {
                        MaskNode(element, sensitiveFields);
                    }
                }

                break;
        }
    }
}
