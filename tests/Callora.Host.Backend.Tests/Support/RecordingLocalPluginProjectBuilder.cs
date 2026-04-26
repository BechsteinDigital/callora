using Callora.Host.Backend.Application.Abstractions.Plugins;

namespace Callora.Host.Backend.Tests.Support;

public sealed class RecordingLocalPluginProjectBuilder : ILocalPluginProjectBuilder
{
    public List<string> BuildCalls { get; } = [];

    public LocalPluginProjectBuildResult NextResult { get; set; } =
        new(true, "ok");

    public Task<LocalPluginProjectBuildResult> BuildAsync(
        string projectPath,
        bool forceRebuild = false,
        CancellationToken cancellationToken = default)
    {
        BuildCalls.Add($"{projectPath}|force={forceRebuild}");
        return Task.FromResult(NextResult);
    }
}
