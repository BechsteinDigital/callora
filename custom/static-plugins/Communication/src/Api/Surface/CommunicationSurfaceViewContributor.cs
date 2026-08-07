using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Plugin.Communication.Api.Surface;

/// <summary>
/// Declares the telephone blocks on the server, under the same ids the browser bundle registers.
/// </summary>
/// <remarks>
/// <para>Two halves of one block, and the split earns its keep twice. <see
/// cref="HostSurfaceViewRegistration.RequiredClaims"/> decides visibility <em>before</em> any markup
/// reaches the visitor, so a phone panel a customer may not see is not merely hidden in their
/// browser. And <see cref="HostSurfaceViewRegistration.RequiresContexts"/> is what makes the context
/// delivery demand-driven: a key no visible block on this surface asked for never leaves the host,
/// however reachable an anchor would make it (design §5.5 P3).</para>
/// <para>Getting an id wrong costs a block that is offered in the editor and never mounts, or a
/// panel that never updates — with nothing anywhere saying why. That is what the governance rule
/// "every client block id has a server registration and vice versa" is for.</para>
/// </remarks>
public sealed class CommunicationSurfaceViewContributor : IHostSurfaceViewContributor
{
    /// <summary>Island id of the ringing-call panel.</summary>
    public const string IncomingCallViewId = "communication.incoming-call";

    /// <summary>Island id of the phone panel.</summary>
    public const string ActiveCallViewId = "communication.active-call";

    /// <summary>Island id of the call list.</summary>
    public const string CallLogViewId = "communication.call-log";

    /// <summary>
    /// Semantic role the phone panels fill. A workplace theme decides where a side panel appears;
    /// the blocks say what they are, not where they go.
    /// </summary>
    public const string PanelSlot = "workspace.panel";

    /// <summary>Semantic role the call list fills.</summary>
    public const string ListSlot = "workspace.main";

    /// <summary>Creates the contributor for a plugin id.</summary>
    /// <param name="pluginId">Stable plugin identifier owning these contributions.</param>
    public CommunicationSurfaceViewContributor(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        PluginId = pluginId;
    }

    /// <inheritdoc />
    public string PluginId { get; }

    /// <inheritdoc />
    public IReadOnlyList<HostSurfaceViewRegistration> Views =>
    [
        new(
            IncomingCallViewId,
            PanelSlot,
            "Eingehender Anruf",
            Weight: 10,
            Cardinality: SurfaceViewCardinality.AtMostOne,
            Description: "Zeigt einen klingelnden Anruf und lässt ihn annehmen oder ablehnen.",
            // Reading is enough to be shown one: answering is refused by the route, and a panel that
            // appears without its buttons working is a worse answer than one that says nothing.
            RequiredClaims: [SurfaceCallAccess.ClaimKey],
            RequiresContexts: [SurfaceCallContextKeys.IncomingCall]),
        new(
            ActiveCallViewId,
            PanelSlot,
            "Telefon",
            Weight: 20,
            Cardinality: SurfaceViewCardinality.AtMostOne,
            Description: "Das laufende Gespräch mit Auflegen und Ziffernblock — und ein Wählfeld, wenn keines läuft.",
            RequiredClaims: [SurfaceCallAccess.ClaimKey],
            RequiresContexts: [SurfaceCallContextKeys.ActiveCall]),
        new(
            CallLogViewId,
            ListSlot,
            "Anrufliste",
            Weight: 30,
            Cardinality: SurfaceViewCardinality.AtMostOne,
            Description: "Die letzten Anrufe des Workspaces, mit erreichter Nummer und Ergebnis.",
            RequiredClaims: [SurfaceCallAccess.ClaimKey],
            // Kein RequiresContexts: Vergangenes ist eine Abfrage. Der Block lauscht zwar auf das
            // Ende eines Gesprächs, aber über den Schlüssel, den das Telefon ohnehin deklariert —
            // ihn hier zu wiederholen hieße, ihn auch auf Flächen auszuliefern, die kein Telefon
            // haben.
            RequiresContexts: null),
    ];
}
