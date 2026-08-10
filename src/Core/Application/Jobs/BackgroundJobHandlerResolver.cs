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
        {
            return null;
        }

        return HostPluginResolution.ResolvePluginWins(
            hostHandlers,
            pluginCatalog.GetExports<IBackgroundJobHandler>(),
            static handler => handler.JobType,
            jobType);
    }

    /// <summary>
    /// Das Plugin, dem der Handler dieses Job-Typs gehört, oder <c>null</c>, wenn ihn der Host
    /// selbst stellt.
    /// </summary>
    /// <remarks>
    /// Ein <see cref="Domain.Jobs.BackgroundJob"/> trägt keine Plugin-Id — er kennt nur seinen
    /// Typ, und das ist richtig so: Wer einen Job einreiht, muss nicht wissen, wer ihn später
    /// ausführt. Für die Fehlerzurechnung fehlt damit aber genau die Angabe, ohne die ein
    /// reihenweise scheiternder Job-Handler niemandem zuzuordnen ist. Der Katalog kennt die
    /// Herkunft jedes Exports; hier wird sie nur nachgeschlagen.
    /// </remarks>
    public string? ResolveOwner(string jobType)
    {
        if (string.IsNullOrWhiteSpace(jobType))
        {
            return null;
        }

        foreach (var owned in pluginCatalog.GetOwnedExports(typeof(IBackgroundJobHandler)))
        {
            if (owned.Service is IBackgroundJobHandler handler &&
                string.Equals(handler.JobType, jobType, StringComparison.OrdinalIgnoreCase))
            {
                return owned.PluginId;
            }
        }

        return null;
    }
}
