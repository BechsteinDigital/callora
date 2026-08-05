using Callora.Core.Application.Persistence;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Plugins;

namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Reads and changes which plugin vouches for a surface's visitors (ADR-017 §5).
/// <para>
/// Two guards make this more than a setter. Only a plugin declaring the
/// <c>surface.identity</c> capability can be assigned — anything else would leave a
/// surface with a provider that can never answer. And every change ends the sessions
/// the previous provider vouched for: once a different party is responsible, keeping
/// the old trust alive would be inconsistent.
/// </para>
/// </summary>
public sealed class SurfaceIdentityAssignmentService(
    IWorkspaceManagementStore workspaceStore,
    IWorkspaceSurfaceStore surfaceStore,
    IPluginInstallationRepository installations,
    IPluginAvailabilityEvaluator availabilityEvaluator,
    ISurfaceSessionStore sessions,
    IPluginPackageRegistryReader? registryReader = null)
{
    /// <summary>Reads the surface's current assignment.</summary>
    /// <param name="workspaceKey">Workspace owning the surface.</param>
    /// <param name="surfaceKey">Surface to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<SurfaceIdentityAssignmentResult> GetAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken = default)
    {
        var surface = await LoadAsync(workspaceKey, surfaceKey, cancellationToken).ConfigureAwait(false);
        return surface is null
            ? await NotFoundAsync(workspaceKey, surfaceKey, cancellationToken).ConfigureAwait(false)
            : new SurfaceIdentityAssignmentResult(
                SurfaceIdentityAssignmentStatus.Ok,
                await ToAssignmentAsync(surface, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Lists the plugins that may be assigned: those declaring the
    /// <c>surface.identity</c> capability, with their availability in the workspace.
    /// </summary>
    /// <param name="workspaceKey">Workspace the assignment would apply in.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyList<SurfaceIdentityProviderCandidate>> ListCandidatesAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);

        var candidates = new List<SurfaceIdentityProviderCandidate>();
        foreach (var installation in await installations.ListAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!DeclaresIdentityCapability(installation))
            {
                continue;
            }

            var availability = await availabilityEvaluator
                .EvaluateAsync(installation.PluginId, workspaceKey, cancellationToken)
                .ConfigureAwait(false);

            candidates.Add(new SurfaceIdentityProviderCandidate(
                installation.PluginId,
                installation.DisplayName,
                await ReadVersionAsync(installation, cancellationToken).ConfigureAwait(false),
                availability.IsAvailable));
        }

        return candidates
            .OrderBy(static x => x.PluginId, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>Assigns a plugin as the surface's identity provider.</summary>
    /// <param name="workspaceKey">Workspace owning the surface.</param>
    /// <param name="surfaceKey">Surface to assign.</param>
    /// <param name="pluginId">Plugin to assign.</param>
    /// <param name="assignedBy">Operator performing the assignment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<SurfaceIdentityAssignmentResult> AssignAsync(
        string workspaceKey,
        string surfaceKey,
        string pluginId,
        string? assignedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        var surface = await LoadAsync(workspaceKey, surfaceKey, cancellationToken).ConfigureAwait(false);
        if (surface is null)
        {
            return await NotFoundAsync(workspaceKey, surfaceKey, cancellationToken).ConfigureAwait(false);
        }

        var installation = await installations
            .GetByPluginIdAsync(pluginId.Trim(), cancellationToken)
            .ConfigureAwait(false);
        if (installation is null)
        {
            return new SurfaceIdentityAssignmentResult(
                SurfaceIdentityAssignmentStatus.PluginNotFound,
                Message: $"Plugin '{pluginId}' is not installed.");
        }

        if (!DeclaresIdentityCapability(installation))
        {
            return new SurfaceIdentityAssignmentResult(
                SurfaceIdentityAssignmentStatus.CapabilityMissing,
                Message: $"Plugin '{pluginId}' does not declare the '{SurfaceIdentityCapability.Key}' capability.");
        }

        var version = await ReadVersionAsync(installation, cancellationToken).ConfigureAwait(false);
        return await StoreAsync(
                surface, installation.PluginId, version, assignedBy, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Removes the surface's identity provider.</summary>
    /// <param name="workspaceKey">Workspace owning the surface.</param>
    /// <param name="surfaceKey">Surface to clear.</param>
    /// <param name="clearedBy">Operator performing the change.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<SurfaceIdentityAssignmentResult> ClearAsync(
        string workspaceKey,
        string surfaceKey,
        string? clearedBy,
        CancellationToken cancellationToken = default)
    {
        var surface = await LoadAsync(workspaceKey, surfaceKey, cancellationToken).ConfigureAwait(false);
        return surface is null
            ? await NotFoundAsync(workspaceKey, surfaceKey, cancellationToken).ConfigureAwait(false)
            : await StoreAsync(surface, null, null, clearedBy, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SurfaceIdentityAssignmentResult> StoreAsync(
        WorkspaceSurfaceSnapshot surface,
        string? pluginId,
        string? version,
        string? assignedBy,
        CancellationToken cancellationToken)
    {
        var stored = await surfaceStore
            .AssignIdentityProviderAsync(
                surface.WorkspaceKey, surface.SurfaceKey, pluginId, version, assignedBy, cancellationToken)
            .ConfigureAwait(false);
        if (stored is null)
        {
            return await NotFoundAsync(surface.WorkspaceKey, surface.SurfaceKey, cancellationToken)
                .ConfigureAwait(false);
        }

        // Sessions the previous provider vouched for end here. The stored assignment
        // timestamp already refuses them on use; this makes the invalidation eager so
        // the rows do not sit around outliving the trust that created them.
        var revoked = await sessions
            .RevokeForSurfaceAsync(surface.WorkspaceKey, surface.SurfaceKey, cancellationToken)
            .ConfigureAwait(false);

        return new SurfaceIdentityAssignmentResult(
            SurfaceIdentityAssignmentStatus.Ok,
            await ToAssignmentAsync(stored, cancellationToken).ConfigureAwait(false),
            RevokedSessions: revoked);
    }

    private async Task<WorkspaceSurfaceSnapshot?> LoadAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(surfaceKey);

        var workspace = await workspaceStore
            .GetAsync(workspaceKey.Trim(), cancellationToken)
            .ConfigureAwait(false);
        return workspace is null
            ? null
            : await surfaceStore
                .GetAsync(workspace.WorkspaceKey, surfaceKey.Trim(), cancellationToken)
                .ConfigureAwait(false);
    }

    private async Task<SurfaceIdentityAssignment> ToAssignmentAsync(
        WorkspaceSurfaceSnapshot surface,
        CancellationToken cancellationToken)
    {
        var available = false;
        if (!string.IsNullOrWhiteSpace(surface.IdentityPluginId))
        {
            var availability = await availabilityEvaluator
                .EvaluateAsync(surface.IdentityPluginId, surface.WorkspaceKey, cancellationToken)
                .ConfigureAwait(false);
            available = availability.IsAvailable;
        }

        return new SurfaceIdentityAssignment(
            surface.WorkspaceKey,
            surface.SurfaceKey,
            surface.IdentityPluginId,
            surface.IdentityVersion,
            surface.IdentityAssignedBy,
            surface.IdentityAssignedAtUtc,
            available);
    }

    private async Task<string?> ReadVersionAsync(
        PluginInstallation installation,
        CancellationToken cancellationToken)
    {
        if (registryReader is null || string.IsNullOrWhiteSpace(installation.AssemblyPath))
        {
            return null;
        }

        // The version is recorded from the package itself rather than taken from the
        // request: what is stored as provenance must be what is actually installed.
        var result = await registryReader
            .ReadForAssemblyAsync(installation.AssemblyPath, cancellationToken)
            .ConfigureAwait(false);
        return result.Registry?.Version;
    }

    private static bool DeclaresIdentityCapability(PluginInstallation installation) =>
        installation.GetProvidedCapabilities()
            .Contains(SurfaceIdentityCapability.Key, StringComparer.OrdinalIgnoreCase);

    // Distinguishing the two is worth one extra read on the error path: "workspace
    // gone" and "surface gone" lead an operator to different places.
    private async Task<SurfaceIdentityAssignmentResult> NotFoundAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken)
    {
        var workspace = await workspaceStore.GetAsync(workspaceKey.Trim(), cancellationToken).ConfigureAwait(false);
        return workspace is null
            ? new SurfaceIdentityAssignmentResult(
                SurfaceIdentityAssignmentStatus.WorkspaceNotFound,
                Message: $"Workspace '{workspaceKey}' not found.")
            : new SurfaceIdentityAssignmentResult(
                SurfaceIdentityAssignmentStatus.SurfaceNotFound,
                Message: $"Surface '{surfaceKey}' not found in workspace '{workspaceKey}'.");
    }
}
