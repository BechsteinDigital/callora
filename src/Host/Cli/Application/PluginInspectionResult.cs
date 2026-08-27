namespace Callora.Host.Cli.Application;

/// <summary>What inspecting a plugin package established.</summary>
/// <param name="IsSuccess">False when the package could not be read at all.</param>
/// <param name="Report">The human-readable report, when it could.</param>
/// <param name="ErrorMessage">Why it could not, otherwise empty.</param>
internal sealed record PluginInspectionResult(bool IsSuccess, string Report, string ErrorMessage)
{
    public static PluginInspectionResult Ok(string report) => new(true, report, string.Empty);

    public static PluginInspectionResult Fail(string errorMessage) => new(false, string.Empty, errorMessage);
}
