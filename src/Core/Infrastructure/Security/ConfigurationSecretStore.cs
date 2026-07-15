using Callora.Core.Application.Secrets.Contracts;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Reads secrets from the "Secrets" configuration section (user secrets,
/// environment configuration providers) — never from committed appsettings.
/// </summary>
public sealed class ConfigurationSecretStore(IConfiguration configuration) : ISecretStore
{
    public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult<string?>(null);

        var value = configuration[$"Secrets:{name.Trim()}"];
        return Task.FromResult(string.IsNullOrEmpty(value) ? null : value);
    }
}
