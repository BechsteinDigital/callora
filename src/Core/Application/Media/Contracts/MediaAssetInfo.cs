namespace Callora.Core.Application.Media.Contracts;

/// <summary>
/// Metadata of one workspace media asset, as returned by <see cref="IMediaLibrary"/>.
/// The bytes are fetched separately via <see cref="IMediaLibrary.OpenReadAsync"/>.
/// </summary>
/// <param name="Id">Stable identifier of the asset.</param>
/// <param name="WorkspaceKey">Workspace the asset belongs to.</param>
/// <param name="FileName">Original file name.</param>
/// <param name="ContentType">MIME type, e.g. "audio/wav".</param>
/// <param name="SizeBytes">Asset size in bytes.</param>
/// <param name="Folder">Logical folder the asset is filed under.</param>
public sealed record MediaAssetInfo(
    Guid Id,
    string WorkspaceKey,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Folder);
