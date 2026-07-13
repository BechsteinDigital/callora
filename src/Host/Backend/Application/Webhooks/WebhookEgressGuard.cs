using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Callora.Host.Backend.Application.Policies;

namespace Callora.Host.Backend.Application.Webhooks;

/// <summary>
/// SSRF guard for outbound webhook targets: resolves the host and rejects
/// loopback, private (RFC 1918), link-local, ULA and multicast addresses.
/// <see cref="EnsureAllowedAsync"/> is the cheap pre-flight check;
/// <see cref="ConnectAsync"/> enforces the same policy at socket-connect
/// time so a DNS answer that changes between check and connect (rebinding)
/// can never reach a private address. Dev environments can opt in to
/// private targets via BackendHostOptions.AllowPrivateWebhookTargets.
/// </summary>
public sealed class WebhookEgressGuard(BackendHostOptions options)
{
    public async Task EnsureAllowedAsync(Uri target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (target.Scheme != Uri.UriSchemeHttps && target.Scheme != Uri.UriSchemeHttp)
        {
            throw new InvalidOperationException("Webhook targets must use http(s).");
        }

        if (options.AllowPrivateWebhookTargets)
        {
            return;
        }

        IPAddress[] addresses;
        if (IPAddress.TryParse(target.Host, out var literal))
        {
            addresses = [literal];
        }
        else
        {
            addresses = await Dns.GetHostAddressesAsync(target.Host, cancellationToken).ConfigureAwait(false);
        }

        if (addresses.Length == 0 || addresses.Any(IsForbidden))
        {
            throw new InvalidOperationException(
                $"Webhook target '{target.Host}' resolves to a non-public address and was blocked.");
        }
    }

    /// <summary>
    /// SocketsHttpHandler.ConnectCallback: resolves the target itself and
    /// connects only to addresses that pass the egress policy, closing the
    /// DNS-rebinding window between validation and connect.
    /// </summary>
    public async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var host = context.DnsEndPoint.Host;
        IPAddress[] addresses;
        if (IPAddress.TryParse(host, out var literal))
        {
            addresses = [literal];
        }
        else
        {
            addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        }

        if (!options.AllowPrivateWebhookTargets)
        {
            addresses = addresses.Where(address => !IsForbidden(address)).ToArray();
        }

        if (addresses.Length == 0)
        {
            throw new HttpRequestException(
                $"Webhook target '{host}' resolves to a non-public address and was blocked.");
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public static bool IsForbidden(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6Multicast ||
            address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var first = address.GetAddressBytes()[0];
            // fc00::/7 (ULA)
            return (first & 0xFE) == 0xFC;
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] switch
        {
            10 => true,                                   // 10.0.0.0/8
            127 => true,                                  // loopback
            169 when bytes[1] == 254 => true,             // 169.254.0.0/16
            172 when bytes[1] >= 16 && bytes[1] <= 31 => true, // 172.16.0.0/12
            192 when bytes[1] == 168 => true,             // 192.168.0.0/16
            >= 224 => true,                               // multicast/reserved
            _ => false
        };
    }
}
