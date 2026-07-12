namespace Callora.Host.Backend.Application.Lifecycle;

public sealed record UpdateNuGetPluginCommand(
    string PluginId,
    string PackageId,
    string PackageVersion,
    string? AssemblyFileName = null,
    string? EntryTypeName = null,
    string? RequestedBy = null);
