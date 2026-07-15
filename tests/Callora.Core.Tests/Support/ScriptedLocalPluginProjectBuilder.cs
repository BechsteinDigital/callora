using Callora.Core.Application.Plugins;

namespace Callora.Core.Tests.Support;

public sealed class ScriptedLocalPluginProjectBuilder : ILocalPluginProjectBuilder
{
    public List<string> BuildCalls { get; } = [];

    public LocalPluginProjectBuildResult NextResult { get; set; } =
        new(true, "ok");

    public Action<string>? AfterBuild { get; set; }

    public Task<LocalPluginProjectBuildResult> BuildAsync(
        string projectPath,
        bool forceRebuild = false,
        CancellationToken cancellationToken = default)
    {
        BuildCalls.Add($"{projectPath}|force={forceRebuild}");
        AfterBuild?.Invoke(projectPath);
        return Task.FromResult(NextResult);
    }
}
