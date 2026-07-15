namespace Callora.Plugin.Communication.Application.Admin;

public static class VoipPermissionKeys
{
    public const string SipAccountCreate = "sipaccount.create";
    public const string SipAccountRead = "sipaccount.read";
    public const string SipAccountUpdate = "sipaccount.update";
    public const string SipAccountDelete = "sipaccount.delete";

    // Call-Berechtigungen gehören zum Voice-Plugin, seit der Call-Stack hier
    // lebt (PLAT-257) — vom Plugin deklariert statt im Host hartkodiert.
    public const string CallRead = "call.read";
    public const string CallExecute = "call.execute";

    public static readonly string[] All =
    [
        SipAccountCreate,
        SipAccountRead,
        SipAccountUpdate,
        SipAccountDelete,
        CallRead,
        CallExecute
    ];
}
