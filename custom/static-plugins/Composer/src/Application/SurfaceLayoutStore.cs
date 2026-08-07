using Callora.Core.Application.Persistence.Contracts;
using Callora.Plugin.Composer.Domain;
using Callora.Plugin.Composer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Callora.Plugin.Composer.Application;

/// <summary>
/// Loads layouts and their versions and runs the transitions against the database. The rules
/// themselves live in <see cref="SurfaceLayoutTransitions"/>; this one only loads, calls and saves.
/// </summary>
public sealed class SurfaceLayoutStore(
    IPluginDbContextFactory<ComposerDbContext> factory,
    TimeProvider timeProvider)
{
    /// <summary>What a layout starts with — no sections, nothing placed.</summary>
    public const string EmptyDocument = """{"sections":[]}""";

    /// <summary>The published version's document for a surface, or null when none is live.</summary>
    public async Task<string?> GetPublishedDocumentAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken = default)
    {
        await using var db = factory.CreateDbContext();

        return await db.Versions
            .AsNoTracking()
            .Where(version => version.State == SurfaceLayoutState.Published)
            .Where(version => db.Layouts
                .Any(layout =>
                    layout.Key == version.LayoutKey &&
                    layout.WorkspaceKey == workspaceKey &&
                    layout.SurfaceKey == surfaceKey))
            .Select(version => version.Document)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// A layout's identity — which workspace and which surface it renders. The editor needs it to
    /// load the right surface's block bundles; the versions alone do not carry it.
    /// </summary>
    public async Task<SurfaceLayout?> GetLayoutAsync(
        string layoutKey,
        CancellationToken cancellationToken = default)
    {
        await using var db = factory.CreateDbContext();
        return await db.Layouts
            .AsNoTracking()
            .SingleOrDefaultAsync(layout => layout.Key == layoutKey, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>The working draft, for the editor.</summary>
    public async Task<SurfaceLayoutVersion?> GetDraftAsync(
        string layoutKey,
        CancellationToken cancellationToken = default)
    {
        await using var db = factory.CreateDbContext();
        return await db.Versions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                version => version.LayoutKey == layoutKey && version.State == SurfaceLayoutState.Draft,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Creates a layout with its first, empty draft.</summary>
    public async Task<SurfaceLayout> CreateAsync(
        string key,
        string workspaceKey,
        string? surfaceKey,
        string name,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        await using var db = factory.CreateDbContext();

        var layout = new SurfaceLayout(key, workspaceKey, surfaceKey, name);
        db.Layouts.Add(layout);
        db.Versions.Add(SurfaceLayoutVersion.NewDraft(
            key, versionNumber: 1, EmptyDocument, createdBy, timeProvider.GetUtcNow()));

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return layout;
    }

    /// <summary>
    /// Writes into the draft. False when the editor's stamp is stale — the second writer gets a
    /// conflict rather than overwriting the first without anyone noticing.
    /// </summary>
    public async Task<bool> TryAutosaveAsync(
        string layoutKey,
        string document,
        DateTimeOffset expectedChangedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var db = factory.CreateDbContext();

        var draft = await LoadDraftAsync(db, layoutKey, cancellationToken).ConfigureAwait(false);
        if (!SurfaceLayoutTransitions.TryAutosave(
                draft, document, expectedChangedAtUtc, timeProvider.GetUtcNow()))
        {
            return false;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Publishes the draft and starts the next one.</summary>
    public async Task PublishAsync(
        string layoutKey,
        string publishedBy,
        string? label,
        CancellationToken cancellationToken = default)
    {
        await using var db = factory.CreateDbContext();

        var draft = await LoadDraftAsync(db, layoutKey, cancellationToken).ConfigureAwait(false);
        var published = await db.Versions
            .SingleOrDefaultAsync(
                version => version.LayoutKey == layoutKey && version.State == SurfaceLayoutState.Published,
                cancellationToken)
            .ConfigureAwait(false);

        var next = SurfaceLayoutTransitions.Publish(
            draft, published, publishedBy, label, timeProvider.GetUtcNow());
        db.Versions.Add(next);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Rebuilds the draft from what is live.</summary>
    public async Task DiscardAsync(
        string layoutKey,
        string discardedBy,
        CancellationToken cancellationToken = default)
    {
        await using var db = factory.CreateDbContext();

        var draft = await LoadDraftAsync(db, layoutKey, cancellationToken).ConfigureAwait(false);
        var published = await db.Versions
            .SingleOrDefaultAsync(
                version => version.LayoutKey == layoutKey && version.State == SurfaceLayoutState.Published,
                cancellationToken)
            .ConfigureAwait(false);

        SurfaceLayoutTransitions.Discard(
            draft, published, EmptyDocument, discardedBy, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Takes an archived version's content into the draft — never straight to live.</summary>
    public async Task RollBackAsync(
        string layoutKey,
        int versionNumber,
        string rolledBackBy,
        CancellationToken cancellationToken = default)
    {
        await using var db = factory.CreateDbContext();

        var draft = await LoadDraftAsync(db, layoutKey, cancellationToken).ConfigureAwait(false);
        var archived = await db.Versions
            .SingleOrDefaultAsync(
                version => version.LayoutKey == layoutKey && version.VersionNumber == versionNumber,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No version {versionNumber} for layout '{layoutKey}'.");

        SurfaceLayoutTransitions.RollBack(draft, archived, rolledBackBy, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<SurfaceLayoutVersion> LoadDraftAsync(
        ComposerDbContext db,
        string layoutKey,
        CancellationToken cancellationToken) =>
        await db.Versions
            .SingleOrDefaultAsync(
                version => version.LayoutKey == layoutKey && version.State == SurfaceLayoutState.Draft,
                cancellationToken)
            .ConfigureAwait(false)
        ?? throw new InvalidOperationException($"Layout '{layoutKey}' has no draft.");
}
