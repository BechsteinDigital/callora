namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// One claimed line, given back on dispose. Idempotent: a double release would hand the same line out
/// twice, and the second holder would be over the quota without anyone having asked for it.
/// </summary>
public sealed class CallQuotaReservation : IDisposable
{
    private readonly CallQuotaLedger _ledger;
    private readonly CallQuotaKey _key;
    private readonly bool _counted;
    private bool _released;

    /// <summary>Creates the reservation; <paramref name="counted"/> is false when no quota applied.</summary>
    public CallQuotaReservation(CallQuotaLedger ledger, CallQuotaKey key, bool counted)
    {
        _ledger = ledger;
        _key = key;
        _counted = counted;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_released || !_counted)
        {
            return;
        }

        _released = true;
        _ledger.Release(_key);
    }
}
