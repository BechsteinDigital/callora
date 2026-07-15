using Callora.Host.PluginContracts.Application.Jobs;
using Callora.Hosting.Application.Plugins;

namespace Callora.Core.Application.Jobs;

/// <summary>
/// Resolves job handlers from host DI registrations and active plugin exports.
/// </summary>
public sealed class BackgroundJobHandlerResolver(
    IEnumerable<IBackgroundJobHandler> hostHandlers,
    ICalloraPluginCatalog pluginCatalog)
{
    /// <summary>
    /// Returns the first handler matching the job type, or null.
    /// </summary>
    public IBackgroundJobHandler? Resolve(string jobType)
    {
        if (string.IsNullOrWhiteSpace(jobType))
            return null;

        return hostHandlers
            .Concat(pluginCatalog.GetExports<IBackgroundJobHandler>())
            .FirstOrDefault(handler => string.Equals(handler.JobType, jobType, StringComparison.OrdinalIgnoreCase));
    }
}
