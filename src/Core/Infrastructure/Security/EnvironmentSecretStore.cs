using Callora.Core.Application.Secrets.Contracts;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Reads secrets from environment variables using the pattern
/// <c>CALLORA_SECRET_&lt;NAME&gt;</c> (name uppercased, non-alphanumerics as underscore).
/// </summary>
public sealed class EnvironmentSecretStore : ISecretStore
{
    private const string Prefix = "CALLORA_SECRET_";

    private readonly Func<string, string?> _readVariable;

    public EnvironmentSecretStore()
        : this(Environment.GetEnvironmentVariable)
    {
    }

    // Testable seam: tests inject a reader instead of mutating a process-global
    // environment variable, which would pollute parallel tests. Production reads the
    // real environment via the parameterless constructor.
    internal EnvironmentSecretStore(Func<string, string?> readVariable)
    {
        _readVariable = readVariable;
    }

    public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult<string?>(null);
        }

        var variableName = Prefix + NormalizeName(name);
        var value = _readVariable(variableName);
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
