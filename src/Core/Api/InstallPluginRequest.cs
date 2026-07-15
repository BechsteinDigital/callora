namespace Callora.Core.Api;

public sealed record InstallPluginRequest(
    string AssemblyPath,
    string? EntryTypeName = null,
    string? RequestedBy = null);
