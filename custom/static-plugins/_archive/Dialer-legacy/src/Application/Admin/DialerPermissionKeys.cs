namespace Callora.Plugins.Dialer.Application.Admin;

public static class DialerPermissionKeys
{
    public const string NumbersRead = "dialer.numbers.read";
    public const string NumbersManage = "dialer.numbers.manage";
    public const string RunsRead = "dialer.runs.read";
    public const string RunsStart = "dialer.runs.start";

    public static readonly string[] All =
    [
        NumbersRead,
        NumbersManage,
        RunsRead,
        RunsStart
    ];
}
