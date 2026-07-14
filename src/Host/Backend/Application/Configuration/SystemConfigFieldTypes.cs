namespace Callora.Host.Backend.Application.Configuration;

/// <summary>
/// Well-known config/theme field types. Secret fields are encrypted at rest
/// and never leave the host in plaintext through public or effective APIs.
/// </summary>
public static class SystemConfigFieldTypes
{
    public const string Secret = "secret";

    public static bool IsSecret(string? fieldType) =>
        string.Equals(fieldType?.Trim(), Secret, StringComparison.OrdinalIgnoreCase);
}
