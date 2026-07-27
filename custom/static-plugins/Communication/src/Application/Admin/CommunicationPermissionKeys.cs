namespace Callora.Plugin.Communication.Application.Admin;

/// <summary>Permission keys contributed by the Communication plugin (operator surface).</summary>
public static class CommunicationPermissionKeys
{
    /// <summary>Read SIP accounts, lines and their status.</summary>
    public const string AccountsRead = "communication.accounts.read";

    /// <summary>Create, update and delete SIP accounts and lines.</summary>
    public const string AccountsManage = "communication.accounts.manage";

    /// <summary>Read call history and live calls.</summary>
    public const string CallsRead = "communication.calls.read";

    /// <summary>Place and hang up calls (call control).</summary>
    public const string CallsManage = "communication.calls.manage";

    /// <summary>All permission keys contributed by the plugin.</summary>
    public static readonly string[] All = [AccountsRead, AccountsManage, CallsRead, CallsManage];
}
