using System.Text.Json;
using Callora.Plugins.Voip.Application.Accounts;

namespace Callora.Plugins.Voip.Application.Admin;

public static class SipAccountRequestParser
{
    public static bool TryParseUpsert(
        JsonElement? body,
        out UpsertSipAccountRequest? request,
        out string? errorMessage)
    {
        request = null;
        errorMessage = null;

        if (body is null || body.Value.ValueKind != JsonValueKind.Object)
        {
            errorMessage = "Request body must be a JSON object.";
            return false;
        }

        var json = body.Value;
        if (!TryReadRequiredString(json, "username", out var username, out errorMessage))
            return false;
        if (!TryReadRequiredString(json, "domain", out var domain, out errorMessage))
            return false;
        if (!TryReadRequiredString(json, "displayName", out var displayName, out errorMessage))
            return false;
        if (!TryReadRequiredString(json, "secret", out var secret, out errorMessage))
            return false;

        var isActive = true;
        if (json.TryGetProperty("isActive", out var isActiveElement))
        {
            if (isActiveElement.ValueKind is JsonValueKind.True)
            {
                isActive = true;
            }
            else if (isActiveElement.ValueKind is JsonValueKind.False)
            {
                isActive = false;
            }
            else
            {
                errorMessage = "Property 'isActive' must be a boolean.";
                return false;
            }
        }

        request = new UpsertSipAccountRequest(
            Username: username,
            Domain: domain,
            DisplayName: displayName,
            Secret: secret,
            IsActive: isActive);
        return true;
    }

    private static bool TryReadRequiredString(
        JsonElement json,
        string propertyName,
        out string value,
        out string? errorMessage)
    {
        value = string.Empty;
        errorMessage = null;

        if (!json.TryGetProperty(propertyName, out var property))
        {
            errorMessage = $"Property '{propertyName}' is required.";
            return false;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            errorMessage = $"Property '{propertyName}' must be a string.";
            return false;
        }

        var candidate = property.GetString()?.Trim() ?? string.Empty;
        if (candidate.Length == 0)
        {
            errorMessage = $"Property '{propertyName}' cannot be empty.";
            return false;
        }

        value = candidate;
        return true;
    }
}
