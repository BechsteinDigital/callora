namespace Callora.Core.Application.Policies;

public sealed class BackendTrustedSignerOptions
{
    public string PublisherId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Thumbprint { get; set; } = string.Empty;

    public string Source { get; set; } = "config";
}
