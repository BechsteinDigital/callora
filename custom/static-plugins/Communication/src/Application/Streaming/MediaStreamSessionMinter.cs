using System.Buffers.Text;
using System.Security.Cryptography;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Domain.Streaming;

namespace Callora.Plugin.Communication.Application.Streaming;

/// <summary>
/// Default <see cref="IMediaStreamSessionMinter"/>: verifies the call against live call tracking,
/// then persists a pending session carrying the hash of a fresh connect token.
/// </summary>
/// <remarks>
/// The ownership check is the whole point of routing minting through an application service. The
/// WebSocket authorizer can only answer "is this token valid" — it has no way to know whether the
/// caller was allowed to obtain it. Checking here, against
/// <see cref="ICallControlService.Get(string, string)"/>, means a ticket exists only for a call the
/// asking workspace actually runs right now.
/// </remarks>
public sealed class MediaStreamSessionMinter(
    ICallControlService callControl,
    IMediaStreamSessionStore sessionStore,
    TimeProvider timeProvider,
    TimeSpan? tokenTimeToLive = null) : IMediaStreamSessionMinter
{
    private readonly TimeSpan _tokenTimeToLive = tokenTimeToLive ?? CommunicationStreamLimits.ConnectTokenTimeToLive;

    /// <summary>
    /// Bytes of entropy in a connect token. The token is a bearer credential guarding a live
    /// conversation, so it is sized like a session secret rather than like an identifier.
    /// </summary>
    private const int TokenEntropyBytes = 32;

    /// <inheritdoc />
    public async Task<MediaStreamTicket?> MintAsync(
        MintMediaStreamCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.WorkspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.CallId);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ConsumerRef);
        if (!Enum.IsDefined(command.Direction))
        {
            throw new ArgumentOutOfRangeException(nameof(command), command.Direction, "Unknown media stream direction.");
        }

        var call = callControl.Get(command.WorkspaceKey, command.CallId);
        if (call is null)
        {
            // Either the workspace does not own this call or it is no longer live. Both are the
            // same answer to the caller: there is nothing here to stream.
            return null;
        }

        var connectToken = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenEntropyBytes));
        var session = new MediaStreamSession(
            id: Guid.CreateVersion7().ToString("n"),
            callId: command.CallId,
            workspaceKey: command.WorkspaceKey,
            consumerRef: command.ConsumerRef,
            connectToken: connectToken,
            format: AudioFormat.G711Ulaw8k20ms,
            direction: command.Direction,
            createdAt: timeProvider.GetUtcNow());

        await sessionStore.AddAsync(session, cancellationToken).ConfigureAwait(false);

        return new MediaStreamTicket(
            session.Id,
            session.CallId,
            connectToken,
            session.Direction,
            (int)_tokenTimeToLive.TotalSeconds);
    }
}
