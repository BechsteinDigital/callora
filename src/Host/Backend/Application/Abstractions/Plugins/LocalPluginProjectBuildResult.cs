namespace Callora.Host.Backend.Application.Abstractions.Plugins;

public sealed record LocalPluginProjectBuildResult(
    bool IsSuccess,
    string Message);
