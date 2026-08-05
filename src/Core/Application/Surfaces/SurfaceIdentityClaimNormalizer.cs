namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Validates the claim bag of an identity candidate against the host's bounds
/// (ADR-017 §3.1). It checks shape and size only — what a claim <em>means</em>
/// belongs to the plugin that issued it, and the host never reads one.
/// </summary>
internal sealed class SurfaceIdentityClaimNormalizer
{
    private readonly SurfaceIdentityOptions _options;

    /// <summary>
    /// Creates the normaliser.
    /// </summary>
    /// <param name="options">Host bounds on claim count, size and shape.</param>
    public SurfaceIdentityClaimNormalizer(SurfaceIdentityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>
    /// Validates and copies the claim bag. The copy matters: a provider must not be
    /// able to mutate claims after the host accepted them.
    /// </summary>
    /// <param name="claims">Candidate claims, possibly null or empty.</param>
    public SurfaceIdentityClaimNormalization Normalize(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? claims)
    {
        if (claims is null || claims.Count == 0)
        {
            return SurfaceIdentityClaimNormalization.Accept(
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));
        }

        if (claims.Count > _options.MaxClaimCount)
        {
            return SurfaceIdentityClaimNormalization.Reject(
                SurfaceIdentityRejectionReason.TooManyClaims,
                $"{claims.Count} claim keys exceed the maximum of {_options.MaxClaimCount}.");
        }

        var accepted = new Dictionary<string, IReadOnlyList<string>>(claims.Count, StringComparer.Ordinal);
        var budget = 0;

        foreach (var (key, values) in claims)
        {
            var keyRejection = ValidateKey(key);
            if (keyRejection is not null)
            {
                return keyRejection;
            }

            var valueRejection = ValidateValues(key, values);
            if (valueRejection is not null)
            {
                return valueRejection;
            }

            budget += key.Length;
            foreach (var value in values)
            {
                budget += value.Length;
            }

            if (budget > _options.MaxClaimTotalLength)
            {
                return SurfaceIdentityClaimNormalization.Reject(
                    SurfaceIdentityRejectionReason.ClaimBudgetExceeded,
                    $"Claims exceed the total budget of {_options.MaxClaimTotalLength} characters.");
            }

            accepted[key] = [.. values];
        }

        return SurfaceIdentityClaimNormalization.Accept(accepted);
    }

    private SurfaceIdentityClaimNormalization? ValidateKey(string key)
    {
        if (SurfaceIdentityIssuers.IsReserved(key))
        {
            return SurfaceIdentityClaimNormalization.Reject(
                SurfaceIdentityRejectionReason.ReservedClaimKey,
                $"Claim key '{key}' uses the reserved host namespace.");
        }

        if (!SurfaceIdentityTokenSyntax.IsNamespacedKey(key, _options.MaxClaimKeyLength))
        {
            return SurfaceIdentityClaimNormalization.Reject(
                SurfaceIdentityRejectionReason.InvalidClaimKey,
                $"Claim key '{key}' is not a valid namespaced key.");
        }

        return null;
    }

    private SurfaceIdentityClaimNormalization? ValidateValues(string key, IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return SurfaceIdentityClaimNormalization.Reject(
                SurfaceIdentityRejectionReason.InvalidClaimValue,
                $"Claim key '{key}' carries no value.");
        }

        if (values.Count > _options.MaxClaimValuesPerKey)
        {
            return SurfaceIdentityClaimNormalization.Reject(
                SurfaceIdentityRejectionReason.TooManyClaimValues,
                $"Claim key '{key}' carries {values.Count} values, exceeding {_options.MaxClaimValuesPerKey}.");
        }

        foreach (var value in values)
        {
            if (!SurfaceIdentityTokenSyntax.IsPrintable(value, _options.MaxClaimValueLength))
            {
                return SurfaceIdentityClaimNormalization.Reject(
                    SurfaceIdentityRejectionReason.InvalidClaimValue,
                    $"Claim key '{key}' carries an oversized or non-printable value.");
            }
        }

        return null;
    }
}
