namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Character-level rules for the identifiers a surface identity carries. Kept as
/// explicit scans rather than patterns so the accepted set is readable and the cost
/// is bounded by the already-length-capped input.
/// </summary>
internal static class SurfaceIdentityTokenSyntax
{
    /// <summary>
    /// A lowercase machine token: issuer, authentication method. Starts and ends
    /// alphanumeric, may contain dot, dash and underscore in between.
    /// </summary>
    /// <param name="value">Candidate value.</param>
    /// <param name="maxLength">Maximum accepted length.</param>
    public static bool IsToken(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length > maxLength)
        {
            return false;
        }

        if (!IsAlphanumeric(value[0]) || !IsAlphanumeric(value[^1]))
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!IsAlphanumeric(c) && c != '.' && c != '-' && c != '_')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// A namespaced claim key such as <c>crm.roles</c>: at least two dot-separated
    /// segments, each starting and ending alphanumeric with dashes allowed inside.
    /// The namespace requirement is what keeps two plugins' claims apart.
    /// </summary>
    /// <param name="value">Candidate key.</param>
    /// <param name="maxLength">Maximum accepted length.</param>
    public static bool IsNamespacedKey(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length > maxLength)
        {
            return false;
        }

        var segments = value.Split('.');
        if (segments.Length < 2)
        {
            return false;
        }

        foreach (var segment in segments)
        {
            if (segment.Length == 0 ||
                !IsAlphanumeric(segment[0]) ||
                !IsAlphanumeric(segment[^1]))
            {
                return false;
            }

            foreach (var c in segment)
            {
                if (!IsAlphanumeric(c) && c != '-')
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Free-form text that may reach a rendered page or a log line: bounded and free
    /// of control characters, which is what keeps it from breaking either.
    /// </summary>
    /// <param name="value">Candidate value.</param>
    /// <param name="maxLength">Maximum accepted length.</param>
    public static bool IsPrintable(string? value, int maxLength)
    {
        if (value is null || value.Length > maxLength)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (char.IsControl(c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAlphanumeric(char c) =>
        c is >= 'a' and <= 'z' or >= '0' and <= '9';
}
