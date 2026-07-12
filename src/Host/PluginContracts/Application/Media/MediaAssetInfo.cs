namespace Callora.Host.PluginContracts.Application.Media;

public sealed record MediaAssetInfo(
    Guid Id,
    string WorkspaceKey,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Folder);
