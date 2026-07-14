using Callora.Host.Backend.Application.Plugins;

namespace Callora.Host.Backend.Tests.Support;

internal sealed class StaticLocalPluginInstallSourceResolver : ILocalPluginInstallSourceResolver
{
    public LocalPluginInstallSourceResolveResult Result { get; set; } =
        new(
            IsSuccess: true,
            PluginId: "plugin-1",
            AssemblyPath: "/tmp/plugin-1.dll",
            EntryTypeName: "Plugin.Entry",
            UsedBuild: false,
            Message: "ok");

    public Task<LocalPluginInstallSourceResolveResult> ResolveForInstallAsync(
        string pluginId,
        bool buildIfNeeded,
        bool forceBuild = false,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result with { PluginId = pluginId });
    }
}
