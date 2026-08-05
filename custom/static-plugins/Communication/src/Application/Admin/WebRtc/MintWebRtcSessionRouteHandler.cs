using System.Text.Json;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Admin.Calls;
using Callora.Plugin.Communication.Application.WebRtc;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Application.Admin.WebRtc;

/// <summary>
/// Handles <c>POST webrtc/sessions</c> — mints the signalling ticket and ICE configuration a browser
/// needs to open a WebRTC call in its workspace (#114). The minter primitive existed but had no
/// production caller, so no browser could legitimately connect.
/// </summary>
/// <remarks>
/// Readiness gates the mint rather than only being reported: a ticket for a surface that cannot carry
/// a call is a two-minute wait ending in a failed socket. When the plugin is unavailable, 503 says so
/// immediately.
/// </remarks>
public sealed class MintWebRtcSessionRouteHandler(
    IWebRtcSessionMinter minter,
    CommunicationReadinessProbe readinessProbe,
    IceConfigurationOptions iceOptions,
    TimeProvider timeProvider,
    ILogger<MintWebRtcSessionRouteHandler> logger) : IHostAdminApiRouteHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!CallAdminScope.TryResolve(request, out var workspaceKey, out var scopeError))
        {
            return scopeError!;
        }

        var status = await readinessProbe.ProbeAsync(cancellationToken).ConfigureAwait(false);
        if (status.Status == CommunicationReadiness.Unavailable)
        {
            return new HostAdminApiResponse(503, new
            {
                error = "Communication is unavailable; no session can be established right now.",
                dependencies = status.Dependencies,
            });
        }

        MintWebRtcSessionApiRequest? body;
        try
        {
            body = request.Body?.Deserialize<MintWebRtcSessionApiRequest>(SerializerOptions);
        }
        catch (JsonException)
        {
            body = null;
        }

        var target = string.IsNullOrWhiteSpace(body?.Target) ? workspaceKey : body!.Target!.Trim();
        var ticket = minter.MintSession(
            workspaceKey,
            new CallTarget(target, body?.DisplayName?.Trim()),
            string.IsNullOrWhiteSpace(body?.CallId) ? null : body!.CallId!.Trim());

        var now = timeProvider.GetUtcNow();
        var iceServers = TurnCredentialFactory.Build(iceOptions, workspaceKey, now);
        var credentialLifetime = iceOptions.Servers.Any(x => x.IssuesShortLivedCredentials)
            ? (int)iceOptions.CredentialTimeToLive.TotalSeconds
            : (int?)null;

        // Audit trail without the credential: the token is what a thief would need, so the line
        // records who minted for which workspace and nothing that could be replayed (#114).
        logger.LogInformation(
            "Minted WebRTC signalling session for workspace {WorkspaceKey} targeting {Target} by user {UserId}.",
            workspaceKey,
            target,
            request.UserId ?? "unknown");

        return new HostAdminApiResponse(201, new WebRtcSessionView(
            ticket.ConnectToken,
            WebRtcSessionView.ConnectPathPrefix + ticket.ConnectToken,
            ticket.ExpiresInSeconds,
            iceServers,
            credentialLifetime));
    }
}
