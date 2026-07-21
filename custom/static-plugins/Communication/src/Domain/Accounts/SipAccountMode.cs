namespace Callora.Plugin.Communication.Domain.Accounts;

/// <summary>How an account connects to the SIP world.</summary>
public enum SipAccountMode
{
    /// <summary>Client registers with a registrar (REGISTER); status reflects the registration.</summary>
    Register = 0,

    /// <summary>IP-authenticated trunk (no REGISTER); status reflects reachability.</summary>
    Trunk = 1
}
