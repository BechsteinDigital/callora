using Callora.Core.Application.Secrets.Contracts;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Resolves secrets from an ordered provider chain; the first non-null value wins.
/// </summary>
public sealed class ChainedSecretStore(IReadOnlyList<ISecretStore> providers) : ISecretStore
{
    public async Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        foreach (var provider in providers)
        {
            var value = await provider.GetSecretAsync(name, cancellationToken).ConfigureAwait(false);
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }
}
