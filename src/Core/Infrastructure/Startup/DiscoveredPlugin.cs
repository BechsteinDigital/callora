namespace Callora.Core.Infrastructure.Startup;

/// <summary>One plugin discovered on disk during a directory scan, before reconciliation.</summary>
internal sealed record DiscoveredPlugin(string PluginId, string AssemblyPath, string? EntryTypeName);
