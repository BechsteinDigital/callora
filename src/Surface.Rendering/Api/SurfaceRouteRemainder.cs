namespace Callora.Surface.Rendering.Api;

/// <summary>
/// Was von einem Anfragepfad übrig bleibt, nachdem die aufgelöste Fläche ihren Teil beansprucht
/// hat.
/// </summary>
/// <remarks>
/// Die Auflösung nimmt das längste passende Präfix und gibt die Fläche zurück — den Rest gab sie
/// nie heraus. Wer ihn braucht, rechnete ihn selbst aus, und wer nicht daran dachte, lieferte
/// eine fremde Seite mit 200 aus.
/// </remarks>
internal static class SurfaceRouteRemainder
{
    /// <summary>
    /// Der Teil des Pfades hinter dem Präfix, ohne führenden und folgenden Schrägstrich, oder
    /// <see cref="string.Empty"/>, wenn der Pfad die Fläche genau trifft.
    /// </summary>
    public static string Of(string? publicPathPrefix, string requestPath)
    {
        var prefix = Normalize(publicPathPrefix);
        var path = Normalize(requestPath);

        if (prefix == "/")
        {
            return path == "/" ? string.Empty : path.TrimStart('/');
        }

        if (string.Equals(path, prefix, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        // An der SEGMENTGRENZE, nicht am Zeichen: `/test/blubber` gehört nicht zu `/test/blub`.
        // Ein reiner Zeichenvergleich meldete „Rest: ber" und lieferte die falsche Seite aus.
        //
        // Kein Treffer heißt: Diese Fläche gehört nicht zu diesem Pfad. Den ganzen Pfad als Rest
        // zu melden ist ehrlicher als eine leere Zeichenkette, die „passt genau" bedeutet — der
        // Aufrufer soll hier nicht stillschweigend rendern.
        if (!path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
        {
            return path.TrimStart('/');
        }

        return path[prefix.Length..].Trim('/');
    }

    private static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "/";
        }

        var path = input.Trim();
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        while (path.Length > 1 && path.EndsWith('/'))
        {
            path = path[..^1];
        }

        return path;
    }
}
