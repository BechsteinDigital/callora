using Callora.Core.Application.Workspaces.Contracts;
using Callora.Core.Domain.Workspaces;

namespace Callora.Core.Application.Workspaces;

internal sealed class WorkspaceSurfaceProvisioner(
    IWorkspaceManagementStore workspaceStore,
    IWorkspaceSurfaceStore surfaceStore) : IWorkspaceSurfaceProvisioner
{
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

        var existing = await surfaceStore
            .GetAsync(normalizedWorkspaceKey, definition.SurfaceKey.Trim(), cancellationToken)
            .ConfigureAwait(false);
        var publicPath = ComposePublicPath(
            workspace.PublicPathPrefix,
            definition.PublicPathSuffix);
        var publicUrl = ComposePublicUrl(
            workspace.PublicBaseUrl,
            workspace.PublicHost,
            publicPath);
        var input = new WorkspaceSurfaceInput(
            definition.SurfaceKey.Trim(),
            definition.DisplayName.Trim(),
            definition.SurfaceType.Trim(),
            publicUrl,
            workspace.PublicHost,
            publicPath,
            ToDomainAccessMode(definition.AccessMode),
            existing?.Locale,
            definition.TemplatePluginId.Trim(),
            string.IsNullOrWhiteSpace(definition.TemplateVersion)
                ? null
                : definition.TemplateVersion.Trim(),
            existing?.ThemePluginId,
            existing?.ThemeVersion,
            IsActive: true);
        var surface = await surfaceStore
            .UpsertAsync(normalizedWorkspaceKey, input, cancellationToken)
            .ConfigureAwait(false);
        return surface is null
            ? null
            : new PluginSurfaceLocation(
                normalizedWorkspaceKey,
                surface.SurfaceKey,
                publicPath,
                publicUrl);
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

    private static string ComposePublicPath(string workspacePrefix, string suffix)
    {
        var prefix = NormalizePath(workspacePrefix);
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
        string? workspacePublicBaseUrl,
        string? publicHost,
        string publicPath)
    {
        if (!string.IsNullOrWhiteSpace(workspacePublicBaseUrl))
        {
            var candidate = workspacePublicBaseUrl.Contains("://", StringComparison.Ordinal)
                ? workspacePublicBaseUrl
                : $"https://{workspacePublicBaseUrl}";
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
