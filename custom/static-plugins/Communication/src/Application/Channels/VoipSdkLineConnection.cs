using CalloraVoipSdk;
using CalloraVoipSdk.Core.Domain.Lines;

namespace Callora.Plugin.Communication.Application.Channels;

/// <summary>
/// One registered SDK client/line pair for a SIP account.
/// </summary>
public sealed record VoipSdkLineConnection(VoipClient Client, IPhoneLine Line);
