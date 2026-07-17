using Callora.Core.Application.Events.Contracts;
using Callora.Core.Application.Flows.Contracts;
using Callora.Core.Application.Jobs.Contracts;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Callora.Core.Infrastructure.DependencyInjection;

/// <summary>
/// Assembly-scan registration for host contract roles — the Symfony
/// autoconfiguration / Shopware <c>AutoconfigureCompilerPass</c> equivalent: a new
/// host implementation of a known contract only has to implement the interface and
/// needs no per-type wiring. Plugin-provided contributors of the same roles come
/// from the plugin catalog at resolve time; this covers only the host side.
/// </summary>
internal static class CalloraContractRegistration
{
    /// <summary>
    /// The host contract roles and their DI lifetime. Lifetimes are per role, not
    /// uniform: job handlers touch scoped persistence and stay scoped; the stateless
    /// evaluators/listeners/providers stay singleton — matching the prior manual
    /// registration exactly.
    /// </summary>
    private static readonly (Type Contract, ServiceLifetime Lifetime)[] ContractRoles =
    [
        (typeof(IBackgroundJobHandler), ServiceLifetime.Scoped),
        (typeof(IRecurringJobProvider), ServiceLifetime.Singleton),
        (typeof(IRuleConditionEvaluator), ServiceLifetime.Singleton),
        (typeof(IFlowActionHandler), ServiceLifetime.Singleton),
        (typeof(IBusinessEventListener), ServiceLifetime.Singleton),
        (typeof(IBusinessEventProvider), ServiceLifetime.Singleton),
    ];

    /// <summary>Registers every non-abstract host implementation of a known contract role.</summary>
    public static IServiceCollection AddCalloraContracts(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        var candidates = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .ToArray();

        foreach (var (contract, lifetime) in ContractRoles)
        {
            foreach (var implementation in candidates.Where(contract.IsAssignableFrom))
            {
                services.Add(new ServiceDescriptor(contract, implementation, lifetime));
            }
        }

        return services;
    }
}
