using Callora.Core.Application.Workspaces.Contracts;
using Callora.Core.Domain.Workspaces;

namespace Callora.Core.Application.Workspaces;

internal sealed class WorkspaceSurfaceProvisioner(
    IWorkspaceManagementStore workspaceStore,
    IWorkspaceSurfaceStore surfaceStore) : IWorkspaceSurfaceProvisioner
{
    /// <summary>The surface every workspace has; plugin surfaces route below it.</summary>
    private const string DefaultSurfaceKey = "default";

    public async Task<PluginSurfaceLocation?> EnsureAsync(
        string workspaceKey,
        PluginSurfaceDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentNullException.ThrowIfNull(definition);
        ValidateDefinition(definition);

        var normalizedWorkspaceKey = workspaceKey.Trim();
        var workspace = await workspaceStore
            .GetAsync(normalizedWorkspaceKey, cancellationToken)
            .ConfigureAwait(false);
        if (workspace is null)
        {
            return null;
        }

        var all = await surfaceStore.ListAsync(normalizedWorkspaceKey, cancellationToken).ConfigureAwait(false);

        // Hat der Betreiber dieser App schon eine Fläche zugewiesen, ist DAS die Fläche.
        //
        // Sonst legte jedes Plugin beim Start eine zweite an, und beide beanspruchten dieselbe
        // Adresse: Wer im Admin „meet" anlegt und ihr die Videokonferenz zuweist, bekam daneben
        // eine „videoconference" mit demselben Pfad. Welche gewinnt, entschied dann die
        // Bewertung — genau der stille Zustand, den ADR-021 abgeschafft hat.
        //
        // Die Zuweisung ist die Entscheidung des Betreibers; das Anlegen ist nur der Notnagel
        // für den Fall, dass er sie noch nicht getroffen hat.
        var assigned = all.FirstOrDefault(surface =>
            string.Equals(surface.TemplatePluginId, definition.TemplatePluginId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (assigned is not null)
        {
            return Locate(assigned, all, normalizedWorkspaceKey, workspace);
        }

        var existing = all.FirstOrDefault(surface =>
            string.Equals(surface.SurfaceKey, definition.SurfaceKey.Trim(), StringComparison.OrdinalIgnoreCase));

        // Eine Plugin-Fläche hängt unter dem Standard-Eingang des Workspaces. Ihr eigener Präfix
        // ist NUR ihr Segment — die Kette rechnet der Server, samt Workspace-Segment und Host
        // (ADR-021). Den fertigen Pfad hier einzutragen ließ `/test` fehlen: Jeder Einladungslink
        // zeigte auf `/meet` und lief in ein 404.
        var input = new WorkspaceSurfaceInput(
            definition.SurfaceKey.Trim(),
            definition.DisplayName.Trim(),
            definition.SurfaceType.Trim(),
            PublicBaseUrl: null,
            PublicHost: null,
            SegmentOf(definition.PublicPathSuffix),
            ToDomainAccessMode(definition.AccessMode),
            existing?.Locale,
            definition.TemplatePluginId.Trim(),
            string.IsNullOrWhiteSpace(definition.TemplateVersion)
                ? null
                : definition.TemplateVersion.Trim(),
            existing?.ThemePluginId,
            existing?.ThemeVersion,
            IsActive: true)
        {
            ParentSurfaceKey = existing?.ParentSurfaceKey ?? DefaultSurfaceKey,
            Routing = definition.Routing == PluginSurfaceRouting.Application
                ? SurfaceRouting.Application
                : SurfaceRouting.Tree,
        };
        var surface = await surfaceStore
            .UpsertAsync(normalizedWorkspaceKey, input, cancellationToken)
            .ConfigureAwait(false);
        if (surface is null)
        {
            return null;
        }

        var updated = await surfaceStore.ListAsync(normalizedWorkspaceKey, cancellationToken).ConfigureAwait(false);
        return Locate(surface, updated, normalizedWorkspaceKey, workspace);
    }

    private static void ValidateDefinition(PluginSurfaceDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.SurfaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.DisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.SurfaceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.PublicPathSuffix);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.TemplatePluginId);

        if (definition.PublicPathSuffix.Contains('?', StringComparison.Ordinal) ||
            definition.PublicPathSuffix.Contains('#', StringComparison.Ordinal) ||
            definition.PublicPathSuffix.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A plugin surface path suffix must be a plain path without query, fragment or parent traversal.",
                nameof(definition));
        }
    }

    /// <summary>
    /// Wo diese Fläche öffentlich liegt — aus der EFFEKTIVEN Sicht, nicht selbst gerechnet.
    /// </summary>
    /// <remarks>
    /// <see cref="EffectiveSurface"/> ist die eine Stelle, an der aus einem Knoten seine Adresse
    /// wird: Kette, Workspace-Segment, geerbter Host. Sie hier ein zweites Mal nachzubauen war
    /// genau der Fehler — der Provisioner setzte den Pfad ohne Workspace-Segment zusammen, und
    /// jeder Einladungslink zeigte auf eine Adresse, die es nicht gibt.
    /// </remarks>
    private static PluginSurfaceLocation Locate(
        WorkspaceSurfaceSnapshot surface,
        IReadOnlyList<WorkspaceSurfaceSnapshot> all,
        string workspaceKey,
        WorkspaceSnapshot workspace)
    {
        var byKey = all.ToDictionary(entry => entry.SurfaceKey, StringComparer.OrdinalIgnoreCase);
        var segments = new List<string?>();
        var host = surface.PublicHost;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var node = surface;
        while (node is not null && seen.Add(node.SurfaceKey))
        {
            segments.Add(node.PublicPathPrefix);
            host ??= node.PublicHost;
            node = node.ParentSurfaceKey is { } parent && byKey.TryGetValue(parent, out var found)
                ? found
                : null;
        }

        host ??= workspace.PublicHost;
        if (host is null)
        {
            segments.Add(workspaceKey);
        }

        var publicPath = SurfaceTree.ComposePath(segments);
        return new PluginSurfaceLocation(
            workspaceKey,
            surface.SurfaceKey,
            publicPath,
            host is null ? publicPath : $"https://{host}{publicPath}");
    }

    /// <summary>Das eigene Segment einer Plugin-Fläche, ohne führenden Schrägstrich.</summary>
    /// <remarks>
    /// Ein Plugin liefert seinen Wunschpfad als <c>/meet</c>. Als eigener Präfix gespeichert
    /// verschluckte der führende Schrägstrich die ganze Kette darüber: Die Fläche lag dann direkt
    /// unter der Wurzel statt unter dem Standard-Eingang des Workspaces.
    /// </remarks>
    private static string SegmentOf(string suffix) => NormalizePath(suffix).TrimStart('/');

    private static string ComposePublicPath(string basePrefix, string suffix)
    {
        var prefix = NormalizePath(basePrefix);
        var normalizedSuffix = NormalizePath(suffix);
        if (normalizedSuffix == "/")
        {
            return prefix;
        }

        return prefix == "/"
            ? normalizedSuffix
            : prefix + normalizedSuffix;
    }

    private static string ComposePublicUrl(
        string? baseUrl,
        string? publicHost,
        string publicPath)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            var candidate = baseUrl.Contains("://", StringComparison.Ordinal)
                ? baseUrl
                : $"https://{baseUrl}";
            if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            {
                return new UriBuilder(uri)
                {
                    Path = publicPath,
                    Query = string.Empty,
                    Fragment = string.Empty,
                }.Uri.ToString().TrimEnd('/');
            }
        }

        return string.IsNullOrWhiteSpace(publicHost)
            ? publicPath
            : $"https://{publicHost}{publicPath}";
    }

    private static string NormalizePath(string value)
    {
        var normalized = value.Trim().Replace('\\', '/');
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        while (normalized.Length > 1 && normalized.EndsWith('/'))
        {
            normalized = normalized[..^1];
        }

        return normalized;
    }

    private static SurfaceAccessMode ToDomainAccessMode(PluginSurfaceAccessMode accessMode) =>
        accessMode switch
        {
            PluginSurfaceAccessMode.Public => SurfaceAccessMode.Public,
            PluginSurfaceAccessMode.Authenticated => SurfaceAccessMode.Authenticated,
            PluginSurfaceAccessMode.Mixed => SurfaceAccessMode.Mixed,
            _ => throw new ArgumentOutOfRangeException(nameof(accessMode)),
        };
}
