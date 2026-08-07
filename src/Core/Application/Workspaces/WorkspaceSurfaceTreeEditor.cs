using Callora.Core.Application.Workspaces.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Callora.Core.Application.Workspaces;

/// <summary>
/// Reicht die vier Baum-Operationen an den Flächen-Store weiter — und nur die.
/// </summary>
/// <remarks>
/// Die Verengung IST die Implementierung: Ein Plugin bekommt diesen Typ aufgelöst, nicht
/// <see cref="IWorkspaceSurfaceStore"/>, und kann damit keine Identity-Provider zuweisen.
/// Ein Cast auf den Store hilft nicht weiter, weil hier keiner durchgereicht wird.
///
/// <para>
/// Pro Aufruf ein eigener Scope, statt einen Store im Konstruktor zu halten: Ein Plugin löst
/// seine Dienste EINMAL beim Start auf, aus dem Root-Provider. Der Flächen-Store ist scoped
/// (er hält einen DbContext), und ein scoped Dienst lässt sich von dort nicht auflösen — der
/// erste Versuch scheiterte genau daran, und zwar beim Aktivieren des Plugins, nicht beim
/// Bauen. Diese Klasse ist deshalb langlebig und öffnet den Scope selbst, wenn sie gebraucht
/// wird.
/// </para>
///
/// <para>
/// Alle Rückgaben sind materialisiert (Listen, Records, ein Enum) — nichts, was den Scope
/// überleben müsste. Käme je eine verzögerte Abfrage dazu, wäre das hier der Ort, an dem sie
/// bricht.
/// </para>
/// </remarks>
public sealed class WorkspaceSurfaceTreeEditor(IServiceScopeFactory scopeFactory) : ISurfaceTreeEditor
{
    private readonly IServiceScopeFactory _scopeFactory =
        scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkspaceSurfaceSnapshot>> ListAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default) =>
        WithStoreAsync(store => store.ListAsync(workspaceKey, cancellationToken));

    /// <inheritdoc />
    public Task<WorkspaceSurfaceSnapshot?> GetAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken = default) =>
        WithStoreAsync(store => store.GetAsync(workspaceKey, surfaceKey, cancellationToken));

    /// <inheritdoc />
    public Task<WorkspaceSurfaceSnapshot?> UpsertAsync(
        string workspaceKey,
        WorkspaceSurfaceInput input,
        CancellationToken cancellationToken = default) =>
        WithStoreAsync(store => store.UpsertAsync(workspaceKey, input, cancellationToken));

    /// <inheritdoc />
    public Task<SurfaceDeleteResult> DeleteAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken = default) =>
        WithStoreAsync(store => store.DeleteAsync(workspaceKey, surfaceKey, cancellationToken));

    private async Task<TResult> WithStoreAsync<TResult>(
        Func<IWorkspaceSurfaceStore, Task<TResult>> operation)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IWorkspaceSurfaceStore>();
        return await operation(store).ConfigureAwait(false);
    }
}
