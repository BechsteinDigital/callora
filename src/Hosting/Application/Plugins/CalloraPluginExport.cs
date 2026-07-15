namespace Callora.Hosting.Application.Plugins;

/// <summary>
/// One exported service together with the id of the plugin that provided it —
/// so consumers can gate or attribute an export by its owning plugin.
/// </summary>
public sealed record CalloraPluginExport(string PluginId, object Service);
