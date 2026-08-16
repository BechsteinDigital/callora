namespace Callora.Core.Application.Snippets;

/// <inheritdoc />
public sealed class SnippetCatalog(ISnippetResolver resolver) : ISnippetCatalog
{
    private IReadOnlyDictionary<string, string> _snippets =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Snippets => _snippets;

    /// <inheritdoc />
    public string Locale { get; private set; } = string.Empty;

    /// <inheritdoc />
    public async Task LoadAsync(
        string? locale,
        string? tenantKey = null,
        string? workspaceKey = null,
        CancellationToken cancellationToken = default)
    {
        _snippets = await resolver
            .ResolveAsync(locale, tenantKey, workspaceKey, cancellationToken)
            .ConfigureAwait(false);
        Locale = locale?.Trim() ?? string.Empty;
    }
}
