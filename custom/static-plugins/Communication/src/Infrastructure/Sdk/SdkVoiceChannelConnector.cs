using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Voice;
using Callora.Plugin.Communication.Domain.Accounts;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// The real <see cref="IVoiceChannelConnector"/>: maps the persisted account
/// (<see cref="SdkSipAccountFactory"/>), registers it through the <see cref="ISdkVoiceRuntime"/> seam,
/// and on success wraps the live line as an <see cref="SdkVoiceChannel"/> whose calls open audio via
/// the runtime's media tap. Returns <see langword="null"/> when the account does not register.
/// </summary>
public sealed class SdkVoiceChannelConnector : IVoiceChannelConnector
{
    private readonly SdkSipAccountFactory _accountFactory;
    private readonly ISdkVoiceRuntime _runtime;
    private readonly string _pluginId;
    private readonly ILogger<SdkVoiceChannelConnector> _logger;

    /// <summary>Creates the connector over the account factory, SDK runtime seam and plugin id.</summary>
    public SdkVoiceChannelConnector(
        SdkSipAccountFactory accountFactory,
        ISdkVoiceRuntime runtime,
        string pluginId,
        ILogger<SdkVoiceChannelConnector> logger)
    {
        ArgumentNullException.ThrowIfNull(accountFactory);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(logger);

        _accountFactory = accountFactory;
        _runtime = runtime;
        _pluginId = pluginId;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IVoiceChannel?> ConnectAsync(SipAccount account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        var sdkAccount = _accountFactory.Create(account);
        var line = await _runtime.ConnectAsync(sdkAccount, cancellationToken).ConfigureAwait(false);
        if (line is null)
        {
            _logger.LogWarning("SIP account {AccountId} did not register.", account.Id);
            return null;
        }

        return new SdkVoiceChannel(account.Id, account.DisplayName, _pluginId, line, _runtime.CreateMediaTap);
    }
}
