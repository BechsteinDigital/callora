namespace Callora.Host.Backend.Application.Abstractions.Media;

public sealed record MediaItemSnapshot(
    Guid Id,
    string WorkspaceKey,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Folder,
    string? CreatedBy,
    DateTimeOffset CreatedAtUtc);
