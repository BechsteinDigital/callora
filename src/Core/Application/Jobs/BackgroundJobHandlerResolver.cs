using Callora.Core.Application.Jobs.Contracts;
using Callora.Core.Application.Plugins;

namespace Callora.Core.Application.Jobs;

/// <summary>
/// Resolves job handlers from host DI registrations and active plugin exports.
/// </summary>
public sealed class BackgroundJobHandlerResolver(
    IEnumerable<IBackgroundJobHandler> hostHandlers,
    ICalloraPluginCatalog pluginCatalog)
{
    /// <summary>
    /// Returns the handler matching the job type, or null. Plugin exports take
    /// precedence over host handlers of the same type (plugin-wins), unless the
    /// host handler is <c>[HostProtected]</c> — see
    /// <see cref="HostPluginResolution.ResolvePluginWins{T}"/>.
    /// </summary>
    public IBackgroundJobHandler? Resolve(string jobType)
    {
        if (string.IsNullOrWhiteSpace(jobType))
            return null;

        return HostPluginResolution.ResolvePluginWins(
            hostHandlers,
            pluginCatalog.GetExports<IBackgroundJobHandler>(),
            static handler => handler.JobType,
            jobType);
    }
}
