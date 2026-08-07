using Callora.Plugin.Communication.Domain.Accounts;

namespace Callora.Plugin.Communication.Application.Admin.SipAccounts;

/// <summary>
/// Turns the quota part of a create/update body into domain values, or into the sentence an operator
/// needs to fix their form.
/// </summary>
/// <remarks>
/// The domain refuses the same things by throwing, which is right for a domain and useless for an API:
/// a 500 tells an operator nothing about which origin they typed twice.
/// </remarks>
public static class CallQuotaValidation
{
    /// <summary>
    /// Validates the sent shares. A <see langword="null"/> list yields none — what an omitted field
    /// means is the caller's decision, because create and update do not mean the same thing by it.
    /// </summary>
    /// <param name="requested">The shares from the body, or null.</param>
    /// <param name="quotas">The validated shares.</param>
    /// <param name="error">Why the shares were refused, when they were.</param>
    public static bool TryBuild(
        IReadOnlyList<CallQuotaRequest>? requested,
        out IReadOnlyList<CallQuota> quotas,
        out string? error)
    {
        quotas = [];
        error = null;

        if (requested is null || requested.Count == 0)
        {
            return true;
        }

        var built = new List<CallQuota>(requested.Count);
        foreach (var entry in requested)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.Origin))
            {
                error = "Every callQuotas entry needs an origin.";
                return false;
            }

            var limit = entry.MaxConcurrentCalls ?? 0;
            if (limit < 1)
            {
                error = $"callQuotas: '{entry.Origin.Trim()}' must allow at least 1 concurrent call.";
                return false;
            }

            built.Add(new CallQuota(entry.Origin, limit));
        }

        try
        {
            quotas = CallQuota.Validate(built);
            return true;
        }
        catch (ArgumentException ex)
        {
            // The duplicate check lives in the domain, so there is one rule and not two that drift.
            error = ex.Message;
            return false;
        }
    }
}
