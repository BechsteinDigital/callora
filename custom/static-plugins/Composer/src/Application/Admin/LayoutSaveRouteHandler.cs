using System.Text.Json;
using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Plugin.Composer.Application.Admin;

/// <summary>
/// Writes into the draft — autosave, which creates no version.
/// <para>
/// A stale stamp answers <c>409</c>, not <c>200</c>. That is the difference between an editor
/// that tells someone their work collided and one that quietly throws half of it away; only the
/// first lets the person decide.
/// </para>
/// </summary>
public sealed class LayoutSaveRouteHandler(SurfaceLayoutStore store) : IHostAdminApiRouteHandler
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.RouteValues.TryGetValue("layoutKey", out var layoutKey) ||
            string.IsNullOrWhiteSpace(layoutKey))
        {
            return new HostAdminApiResponse(400, new { error = "layoutKey is required." });
        }

        if (request.Body is not { } body)
        {
            return new HostAdminApiResponse(400, new { error = "A body is required." });
        }

        LayoutSaveRequest? save;
        try
        {
            save = body.Deserialize<LayoutSaveRequest>(Options);
        }
        catch (JsonException)
        {
            return new HostAdminApiResponse(400, new { error = "The body could not be read." });
        }

        if (save is null)
        {
            return new HostAdminApiResponse(400, new { error = "The body could not be read." });
        }

        // Ein Layout-Dokument ist ein Objekt. Eine Zahl oder ein String ist syntaktisch gültiges
        // JSON und trotzdem kein Layout — ohne diese Prüfung landete er in der Datenbank, und
        // erst der Renderer fiele darüber, bei einem Besucher.
        if (save.Document.ValueKind != JsonValueKind.Object)
        {
            return new HostAdminApiResponse(400, new { error = "The document must be an object." });
        }

        var saved = await store
            .TryAutosaveAsync(
                layoutKey,
                save.Document.GetRawText(),
                save.ExpectedChangedAtUtc,
                cancellationToken)
            .ConfigureAwait(false);

        return saved
            ? new HostAdminApiResponse(204)
            : new HostAdminApiResponse(409, new
            {
                error = "The draft changed since it was loaded.",
                // Womit, sagt die Antwort NICHT. Wer den Konflikt sieht, lädt neu und
                // entscheidet; den fremden Stand mitzuschicken lüde dazu ein, ihn im Client zu
                // mischen — und das ist die Stelle, an der Arbeit verschwindet.
            });
    }
}
