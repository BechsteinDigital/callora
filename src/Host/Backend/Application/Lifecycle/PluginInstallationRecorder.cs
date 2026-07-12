using Callora.Host.Backend.Application.Abstractions.Persistence;
using Callora.Host.Backend.Domain.Plugins;

namespace Callora.Host.Backend.Application.Lifecycle;

/// <summary>
/// Persists plugin installation state transitions in the host database.
/// </summary>
public sealed class PluginInstallationRecorder(
    IPluginInstallationRepository installationRepository,
    IHostUnitOfWork unitOfWork)
{
    /// <summary>
    /// Upserts one installation row and applies a state transition callback.
    /// </summary>
    public async Task MarkAsync(
        string pluginId,
        string displayName,
        string assemblyPath,
        string? entryTypeName,
        Action<PluginInstallation, DateTimeOffset> mark,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var installation = await installationRepository.GetByPluginIdAsync(pluginId, cancellationToken).ConfigureAwait(false);

        if (installation is null)
        {
            var safeAssemblyPath = string.IsNullOrWhiteSpace(assemblyPath) ? "unknown" : assemblyPath;
            installation = PluginInstallation.CreateInstalled(
                pluginId,
                displayName,
                safeAssemblyPath,
                entryTypeName,
                now);

            await installationRepository.AddAsync(installation, cancellationToken).ConfigureAwait(false);
        }
        else if (!string.IsNullOrWhiteSpace(assemblyPath))
        {
            installation.ApplyInstallMetadata(displayName, assemblyPath, entryTypeName, now);
        }

        mark(installation, now);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Upserts one installation row in installed state without further transitions.
    /// </summary>
    public async Task RecordInstalledAsync(
        string pluginId,
        string displayName,
        string assemblyPath,
        string? entryTypeName,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? providedCapabilities = null,
        IReadOnlyList<string>? requiredCapabilities = null)
    {
        var now = DateTimeOffset.UtcNow;
        var installation = await installationRepository.GetByPluginIdAsync(pluginId, cancellationToken).ConfigureAwait(false);

        if (installation is null)
        {
            installation = PluginInstallation.CreateInstalled(
                pluginId,
                displayName,
                assemblyPath,
                entryTypeName,
                now);

            await installationRepository.AddAsync(installation, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            installation.ApplyInstallMetadata(displayName, assemblyPath, entryTypeName, now);
        }

        installation.SetCapabilities(providedCapabilities, requiredCapabilities, now);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Restores the persisted installation state after a rollback to a previous plugin version.
    /// </summary>
    public Task RestoreAsync(
        string pluginId,
        string displayName,
        string assemblyPath,
        string? entryTypeName,
        PluginInstallationState previousState,
        CancellationToken cancellationToken)
    {
        return previousState switch
        {
            PluginInstallationState.Active => MarkAsync(
                pluginId,
                displayName,
                assemblyPath,
                entryTypeName,
                static (x, now) => x.MarkActivated(now),
                cancellationToken),
            PluginInstallationState.Inactive => MarkAsync(
                pluginId,
                displayName,
                assemblyPath,
                entryTypeName,
                static (x, now) => x.MarkDeactivated(now),
                cancellationToken),
            _ => RecordInstalledAsync(
                pluginId,
                displayName,
                assemblyPath,
                entryTypeName,
                cancellationToken)
        };
    }
}
