namespace Callora.Host.Backend.Application.Abstractions.Workspaces;

public static class WorkspacePublicUrlNormalizer
{
    public static bool TryNormalize(
        string? rawPublicBaseUrl,
        out WorkspacePublicUrlDescriptor descriptor,
        out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(rawPublicBaseUrl))
        {
            descriptor = new WorkspacePublicUrlDescriptor(null, null, "/");
            errorMessage = null;
            return true;
        }

        var normalizedInput = rawPublicBaseUrl.Trim();
        if (normalizedInput.StartsWith('/'))
        {
            descriptor = new WorkspacePublicUrlDescriptor(
                normalizedInput,
                null,
                NormalizePath(normalizedInput));
            errorMessage = null;
            return true;
        }

        var parseInput = normalizedInput.Contains("://", StringComparison.Ordinal)
            ? normalizedInput
            : $"https://{normalizedInput}";

        if (!Uri.TryCreate(parseInput, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
        {
            descriptor = new WorkspacePublicUrlDescriptor(null, null, "/");
            errorMessage = "PublicBaseUrl must be a valid host or host/path (for example dialer.example.de or localhost/dialer).";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(uri.Query) || !string.IsNullOrWhiteSpace(uri.Fragment))
        {
            descriptor = new WorkspacePublicUrlDescriptor(null, null, "/");
            errorMessage = "PublicBaseUrl must not contain query string or fragment.";
            return false;
        }

        var host = uri.IsDefaultPort
            ? uri.Host.ToLowerInvariant()
            : $"{uri.Host}:{uri.Port}".ToLowerInvariant();
        var pathPrefix = NormalizePath(uri.AbsolutePath);

        descriptor = new WorkspacePublicUrlDescriptor(
            normalizedInput,
            host,
            pathPrefix);
        errorMessage = null;
        return true;
    }

    private static string NormalizePath(string? input)
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

        while (path.Length > 1 && path.EndsWith("/", StringComparison.Ordinal))
        {
            path = path[..^1];
        }

        return path.Length == 0 ? "/" : path;
    }
}
