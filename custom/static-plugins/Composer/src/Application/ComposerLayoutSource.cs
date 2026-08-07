using System.Text.Json;
using Callora.Core.Application.Surfaces.Layout;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Composer.Application;

/// <summary>
/// The Composer's answer to the core's <c>ISurfaceLayoutSource</c>: it owns the data, the core
/// only knows the contract. No composer installed → no layout → a surface renders from
/// <c>.njk</c> exactly as before.
/// </summary>
public sealed class ComposerLayoutSource(
    SurfaceLayoutStore store,
    ILogger<ComposerLayoutSource> logger) : ISurfaceLayoutSource
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<SurfaceLayoutDocument?> GetPublishedAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken = default)
    {
        var document = await store
            .GetPublishedDocumentAsync(workspaceKey, surfaceKey, cancellationToken)
            .ConfigureAwait(false);

        return document is null ? null : Deserialize(document, surfaceKey);
    }

    /// <inheritdoc />
    public Task<IReadOnlySet<string>> ListPublishedSurfaceKeysAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default) =>
        store.ListPublishedSurfaceKeysAsync(workspaceKey, cancellationToken);

    /// <inheritdoc />
    public async Task<SurfaceLayoutDocument?> GetDraftAsync(
        string layoutKey,
        CancellationToken cancellationToken = default)
    {
        var draft = await store.GetDraftAsync(layoutKey, cancellationToken).ConfigureAwait(false);
        return draft is null ? null : Deserialize(draft.Document, layoutKey);
    }

    /// <summary>
    /// A stored document that no longer parses renders nothing rather than taking the surface
    /// down — same shape as the template fallback. It can happen: the document was written by an
    /// older editor, and a visitor should not pay for that.
    /// </summary>
    private SurfaceLayoutDocument? Deserialize(string json, string context)
    {
        try
        {
            return JsonSerializer.Deserialize<SurfaceLayoutDocument>(json, Options);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Stored layout document for {Context} could not be read.", context);
            return null;
        }
    }
}
