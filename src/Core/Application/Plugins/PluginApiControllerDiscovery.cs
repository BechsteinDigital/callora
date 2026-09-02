using System.Reflection;
using Callora.Core.Application.Http.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// Findet die <see cref="IApiController"/>-Typen einer Plugin-Assembly und exportiert sie.
/// </summary>
/// <remarks>
/// Shopware-artige Controller-Discovery (PLAT-257): Ein Plugin muss seine Controller nicht von Hand
/// exportieren — wer einen schreibt, hat ihn damit angemeldet. Herausgelöst aus
/// <see cref="RuntimePluginHost"/>, weil es eine eigene Frage beantwortet und die Datei dort an ihrer
/// Zeilengrenze steht.
/// </remarks>
internal static class PluginApiControllerDiscovery
{
    /// <summary>
    /// Instanziiert jeden gefundenen Controller über die kuratierte Dienstoberfläche des Plugins und
    /// meldet ihn über <paramref name="export"/> an.
    /// </summary>
    /// <param name="alreadyExported">
    /// Typen, die das Plugin in <c>StartAsync</c> selbst exportiert hat. Die werden übersprungen, sonst
    /// stünde derselbe Controller zweimal da — einmal mit den Abhängigkeiten, die das Plugin ihm gab,
    /// und einmal mit denen, die hier geraten wurden.
    /// </param>
    internal static void Register(
        string pluginId,
        Assembly pluginAssembly,
        IServiceProvider pluginServices,
        IReadOnlySet<Type> alreadyExported,
        Action<string, Type, object> export,
        ILogger logger)
    {
        foreach (var controllerType in TypesOf(pluginAssembly))
        {
            if (controllerType.IsAbstract ||
                controllerType.IsInterface ||
                !typeof(IApiController).IsAssignableFrom(controllerType) ||
                alreadyExported.Contains(controllerType))
            {
                continue;
            }

            // Ctor-Fehler (nicht kuratierter Service) lassen die Aktivierung bewusst laut scheitern
            // statt Routen still zu verlieren.
            export(pluginId, typeof(IApiController), ActivatorUtilities.CreateInstance(pluginServices, controllerType));

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Registered API controller {ControllerType} for plugin {PluginId}.",
                    controllerType.FullName,
                    pluginId);
            }
        }
    }

    /// <summary>
    /// Die Typen der Assembly — und bei einem Ladefehler die, die trotzdem gelesen werden konnten.
    /// </summary>
    /// <remarks>
    /// Ein Plugin, dessen eine Klasse auf einen fehlenden Typ zeigt, verliert sonst alle Controller
    /// statt nur den einen.
    /// </remarks>
    private static Type[] TypesOf(Assembly pluginAssembly)
    {
        try
        {
            return pluginAssembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return [.. exception.Types.Where(static type => type is not null)!];
        }
    }
}
