namespace Callora.Host.Backend.Application.Plugins;

public sealed record LocalPluginProjectBuildResult(
    bool IsSuccess,
    string Message);
