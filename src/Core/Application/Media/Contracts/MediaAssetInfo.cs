namespace Callora.Core.Application.Media.Contracts;

public sealed record MediaAssetInfo(
    Guid Id,
    string WorkspaceKey,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Folder);
