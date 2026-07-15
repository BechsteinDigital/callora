namespace Callora.Core.Application.Plugins;

public sealed record LocalPluginProjectBuildResult(
    bool IsSuccess,
    string Message);
