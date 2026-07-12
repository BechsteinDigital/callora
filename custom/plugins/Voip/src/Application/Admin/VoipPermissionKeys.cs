namespace Callora.Plugins.Voip.Application.Admin;

public static class VoipPermissionKeys
{
    public const string SipAccountCreate = "sipaccount.create";
    public const string SipAccountRead = "sipaccount.read";
    public const string SipAccountUpdate = "sipaccount.update";
    public const string SipAccountDelete = "sipaccount.delete";

    public static readonly string[] All =
    [
        SipAccountCreate,
        SipAccountRead,
        SipAccountUpdate,
        SipAccountDelete
    ];
}
