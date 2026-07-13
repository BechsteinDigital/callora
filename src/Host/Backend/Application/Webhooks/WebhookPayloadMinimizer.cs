using System.Text.Json;
using System.Text.Json.Nodes;

namespace Callora.Host.Backend.Application.Webhooks;

/// <summary>
/// Data minimization for outbound webhook payloads (PLAT-244): masks values
/// of known person-related fields (phone numbers, display names, e-mail
/// addresses) recursively before the payload leaves the platform.
/// Subscriptions opt in to unmasked payloads explicitly.
/// </summary>
public static class WebhookPayloadMinimizer
{
    private static readonly HashSet<string> SensitiveFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "targetValue",
        "targetDisplayName",
        "target",
        "phoneNumber",
        "callerNumber",
        "calleeNumber",
        "displayName",
        "email"
    };

    public static string Minimize(string bodyJson)
    {
        ArgumentNullException.ThrowIfNull(bodyJson);

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

        MaskNode(root);
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

    private static void MaskNode(JsonNode node)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var propertyName in jsonObject.Select(static property => property.Key).ToArray())
                {
                    var child = jsonObject[propertyName];
                    if (child is JsonValue value &&
                        SensitiveFieldNames.Contains(propertyName) &&
                        value.TryGetValue<string>(out var text))
                    {
                        jsonObject[propertyName] = MaskValue(text);
                        continue;
                    }

                    if (child is not null)
                    {
                        MaskNode(child);
                    }
                }

                break;
            case JsonArray jsonArray:
                foreach (var element in jsonArray)
                {
                    if (element is not null)
                    {
                        MaskNode(element);
                    }
                }

                break;
        }
    }
}
