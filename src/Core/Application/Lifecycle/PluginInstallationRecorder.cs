using Callora.Core.Application.Persistence;
using Callora.Core.Application.Plugins;
using Callora.Core.Domain.Plugins;

namespace Callora.Core.Application.Lifecycle;

/// <summary>
/// Persists plugin installation state transitions in the host database.
/// </summary>
/// <remarks>
/// Der einzige Ort, an dem ein Assembly-Pfad in die Datenbank kommt — und damit der Ort, an dem
/// er portabel gemacht wird (#307). Ohne <paramref name="pathPortability"/> wird gespeichert, was
/// hereinkommt; das ist das Verhalten von vorher und gilt nur für von Hand zusammengesetzte
/// Aufbauten, nicht für den Host.
/// </remarks>
public sealed class PluginInstallationRecorder(
    IPluginInstallationRepository installationRepository,
    IHostUnitOfWork unitOfWork,
    IPluginAssemblyPathPortability? pathPortability = null)
{
    private string ToStoredPath(string assemblyPath)
        => pathPortability is null ? assemblyPath : pathPortability.ToStoredPath(assemblyPath);

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
            var safeAssemblyPath = string.IsNullOrWhiteSpace(assemblyPath) ? "unknown" : ToStoredPath(assemblyPath);
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
            installation.ApplyInstallMetadata(displayName, ToStoredPath(assemblyPath), entryTypeName, now);
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
        IReadOnlyList<string>? requiredCapabilities = null,
        IReadOnlyList<string>? conditionalCapabilities = null)
    {
        var now = DateTimeOffset.UtcNow;
        var installation = await installationRepository.GetByPluginIdAsync(pluginId, cancellationToken).ConfigureAwait(false);

        if (installation is null)
        {
            installation = PluginInstallation.CreateInstalled(
                pluginId,
                displayName,
                ToStoredPath(assemblyPath),
                entryTypeName,
                now);

            await installationRepository.AddAsync(installation, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            installation.ApplyInstallMetadata(displayName, ToStoredPath(assemblyPath), entryTypeName, now);
        }

        installation.SetCapabilities(providedCapabilities, requiredCapabilities, conditionalCapabilities, now);
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
