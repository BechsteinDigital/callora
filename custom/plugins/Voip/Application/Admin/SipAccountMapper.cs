namespace Callora.Plugins.Voip.Application.Admin;

public static class SipAccountMapper
{
    public static SipAccountApiModel ToApiModel(SipAccountEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new SipAccountApiModel(
            entry.SipAccountId,
            entry.Username,
            entry.Domain,
            entry.DisplayName,
            entry.IsActive,
            entry.CreatedAtUtc,
            entry.UpdatedAtUtc);
    }
}
