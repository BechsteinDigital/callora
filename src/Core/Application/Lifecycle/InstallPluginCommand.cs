namespace Callora.Core.Application.Lifecycle;

public sealed record InstallPluginCommand(
    string AssemblyPath,
    string? EntryTypeName = null,
    string? RequestedBy = null);
