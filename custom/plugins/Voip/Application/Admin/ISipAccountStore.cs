namespace Callora.Plugins.Voip.Application.Admin;

public interface ISipAccountStore
{
    IReadOnlyList<SipAccountEntry> List();

    SipAccountEntry? Get(string sipAccountId);

    SipAccountEntry Create(UpsertSipAccountRequest request);

    SipAccountEntry? Update(string sipAccountId, UpsertSipAccountRequest request);

    bool Delete(string sipAccountId);
}
