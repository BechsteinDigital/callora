using Microsoft.Extensions.Configuration;

namespace Callora.Plugin.Communication.Application.WebRtc;

/// <summary>
/// ICE settings handed to browser peers, read from the plugin-scoped <c>WebRtc:IceServers</c> section
/// — the same section the server-side client uses, extended by the fields only a browser needs.
/// </summary>
/// <param name="Servers">The configured STUN/TURN servers. Empty means host candidates only.</param>
/// <param name="CredentialTimeToLive">
/// How long a derived TURN credential stays valid. Long enough to complete ICE on a slow network,
/// short enough that a credential lifted from a browser is worthless by the time it is replayed.
/// </param>
public sealed record IceConfigurationOptions(
    IReadOnlyList<IceServerSetting> Servers,
    TimeSpan CredentialTimeToLive)
{
    /// <summary>Default credential lifetime when the deployment names none.</summary>
    public static readonly TimeSpan DefaultCredentialTimeToLive = TimeSpan.FromMinutes(10);

    /// <summary>Nothing configured: the browser gathers host candidates only.</summary>
    public static IceConfigurationOptions None { get; } = new([], DefaultCredentialTimeToLive);

    /// <summary>
    /// Reads the section. An entry without a host is skipped rather than failing the load, matching
    /// how the server-side client reads the same list.
    /// </summary>
    public static IceConfigurationOptions FromConfiguration(IConfiguration? configuration)
    {
        if (configuration is null)
        {
            return None;
        }

        var section = configuration.GetSection("WebRtc");
        var servers = new List<IceServerSetting>();
        foreach (var entry in section.GetSection("IceServers").GetChildren())
        {
            var host = entry["Host"];
            if (string.IsNullOrWhiteSpace(host))
            {
                continue;
            }

            servers.Add(new IceServerSetting(
                BuildUrl(host.Trim(), entry["Port"], entry["Type"], entry["Transport"]),
                entry["SharedSecret"],
                entry["Username"],
                entry["Password"]));
        }

        var ttlSeconds = int.TryParse(section["CredentialTimeToLiveSeconds"], out var parsed) && parsed > 0
            ? TimeSpan.FromSeconds(parsed)
            : DefaultCredentialTimeToLive;

        return new IceConfigurationOptions(servers, ttlSeconds);
    }

    // RFC 7064/7065 URL form. The scheme carries the security tier (turns/stuns for TLS), and the
    // transport query is meaningful for TURN only — a STUN URL with one would be malformed.
    private static string BuildUrl(string host, string? port, string? type, string? transport)
    {
        var isTurn = string.Equals(type, "turn", StringComparison.OrdinalIgnoreCase);
        var isTls = string.Equals(transport, "tls", StringComparison.OrdinalIgnoreCase);
        var scheme = (isTurn, isTls) switch
        {
            (true, true) => "turns",
            (true, false) => "turn",
            (false, true) => "stuns",
            (false, false) => "stun",
        };

        var url = string.IsNullOrWhiteSpace(port) ? $"{scheme}:{host}" : $"{scheme}:{host}:{port.Trim()}";
        if (!isTurn)
        {
            return url;
        }

        // TLS is expressed by the turns scheme; the transport parameter then names the underlying
        // transport, which for turns is always TCP.
        var turnTransport = isTls
            ? "tcp"
            : string.Equals(transport, "tcp", StringComparison.OrdinalIgnoreCase) ? "tcp" : "udp";
        return $"{url}?transport={turnTransport}";
    }
}
