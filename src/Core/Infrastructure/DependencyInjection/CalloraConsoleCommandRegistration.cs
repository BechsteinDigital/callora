using System.Reflection;
using Callora.Core.Application.Cli;
using Microsoft.Extensions.DependencyInjection;

namespace Callora.Core.Infrastructure.DependencyInjection;

/// <summary>
/// Assembly-scan registration for console commands — the Symfony
/// <c>console.command</c> auto-discovery equivalent, so a new command only has to
/// implement <see cref="ICalloraConsoleCommand"/> and needs no per-command wiring.
/// </summary>
internal static class CalloraConsoleCommandRegistration
{
    /// <summary>Registers every non-abstract command in <paramref name="assembly"/> as scoped.</summary>
    public static IServiceCollection AddCalloraConsoleCommands(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (var commandType in assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(ICalloraConsoleCommand).IsAssignableFrom(type)))
        {
            services.AddScoped(typeof(ICalloraConsoleCommand), commandType);
        }

        return services;
    }
}
