using Callora.Core.Application.Secrets.Contracts;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Reads secrets from environment variables using the pattern
/// <c>CALLORA_SECRET_&lt;NAME&gt;</c> (name uppercased, non-alphanumerics as underscore).
/// </summary>
public sealed class EnvironmentSecretStore : ISecretStore
{
    private const string Prefix = "CALLORA_SECRET_";

    public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult<string?>(null);
        }

        var variableName = Prefix + NormalizeName(name);
        var value = Environment.GetEnvironmentVariable(variableName);
        return Task.FromResult(string.IsNullOrEmpty(value) ? null : value);
    }

    private static string NormalizeName(string name)
    {
        var normalized = name.Trim().ToUpperInvariant()
            .Select(static ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray();
        return new string(normalized);
    }
}
