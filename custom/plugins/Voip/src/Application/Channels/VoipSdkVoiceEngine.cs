using Callora.Contracts.Communication;
using Callora.Plugins.Voip.Application.Accounts;
using CalloraVoipSdk;
using CalloraVoipSdk.Core.Domain.Lines;
using CalloraVoipSdk.Core.Security;

namespace Callora.Plugins.Voip.Application.Channels;

/// <summary>
/// Engine implementation backed by CalloraVoipSdk. Maintains one registered
/// line per SIP account and dials over it. This is the only type in the
/// plugin touching the SDK client facade.
/// </summary>
public sealed class VoipSdkVoiceEngine : IVoiceEngine
{
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly Dictionary<string, VoipSdkLineConnection> _connections = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _disposed;

    public async Task<IEngineCall> PlaceCallAsync(
        SipAccountEntry account,
        CallTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(target);

        var connection = await GetOrConnectAsync(account, cancellationToken).ConfigureAwait(false);
        var targetUri = BuildTargetUri(account, target);

        var call = await connection.Line
            .DialAsync(targetUri, options: null, cancellationToken)
            .ConfigureAwait(false);

        return new VoipSdkEngineCall(call, connection.Client.Media);
    }

    public async Task<IDisposable> SubscribeIncomingCallsAsync(
        SipAccountEntry account,
        Action<IEngineCall> onIncomingCall,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(onIncomingCall);

        var connection = await GetOrConnectAsync(account, cancellationToken).ConfigureAwait(false);
        return connection.Client.OnIncomingCall(call =>
        {
            onIncomingCall(new VoipSdkEngineCall(call, connection.Client.Media));
            return Task.CompletedTask;
        });
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await _connectLock.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var connection in _connections.Values)
            {
                await ShutdownConnectionAsync(connection).ConfigureAwait(false);
            }

            _connections.Clear();
        }
        finally
        {
            _connectLock.Release();
            _connectLock.Dispose();
        }
    }

    private async Task<VoipSdkLineConnection> GetOrConnectAsync(
        SipAccountEntry account,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connections.TryGetValue(account.SipAccountId, out var existing))
                return existing;

            var client = new VoipClient(BuildRegistrarFriendlyConfiguration());
            try
            {
                var connect = await client
                    .ConnectAsync(BuildSipAccount(account), options: null, cancellationToken)
                    .ConfigureAwait(false);

                if (!connect.IsSuccess || connect.Line is null)
                {
                    throw new InvalidOperationException(
                        $"SIP registration for account '{account.SipAccountId}' failed with status '{connect.Status}'.",
                        connect.Error);
                }

                var connection = new VoipSdkLineConnection(client, connect.Line);
                _connections[account.SipAccountId] = connection;
                return connection;
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private static SdkConfiguration BuildRegistrarFriendlyConfiguration() =>
        new()
        {
            // Consumer registrars (FritzBox) and classic provider trunks reject
            // SDP offers carrying SRTP crypto attributes with 488; plain RTP
            // with G.711-first keeps the broadest compatibility. SRTP becomes
            // a per-account option once a trunk requires it.
            SrtpPolicy = SrtpPolicy.Disabled,
            PreferredAudioCodecs = ["PCMA", "PCMU", "G722"]
        };

    private static SipAccount BuildSipAccount(SipAccountEntry account) =>
        new()
        {
            Username = account.Username,
            Password = account.Secret,
            SipServer = account.Domain,
            DisplayName = account.DisplayName
        };

    private static string BuildTargetUri(SipAccountEntry account, CallTarget target)
    {
        var value = target.Value.Trim();
        return value.StartsWith("sip:", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"sip:{value}@{account.Domain}";
    }

    private static async ValueTask ShutdownConnectionAsync(VoipSdkLineConnection connection)
    {
        try
        {
            await connection.Line.UnregisterAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Unregister is best effort during shutdown; disposing the client below
            // releases sockets and timers even when the registrar is unreachable.
        }
        finally
        {
            connection.Client.Dispose();
        }
    }
}
