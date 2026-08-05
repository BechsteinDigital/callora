using Callora.Core.Application.Events.Contracts;
using Callora.Core.Application.Flows.Contracts;
using Callora.Core.Application.Jobs.Contracts;
using Callora.Core.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Callora.Core.Tests.Infrastructure.DependencyInjection;

/// <summary>
/// Locks the host contract roles the assembly-scan registrar wires up (R1): the
/// autoconfiguration equivalent must register exactly the known host
/// implementations, each with the correct per-role lifetime — so the manual
/// registration it replaces stays verifiably in sync.
/// </summary>
public sealed class CalloraContractRegistrationTests
{
    private static ServiceCollection Register()
    {
        var services = new ServiceCollection();
        services.AddCalloraContracts(typeof(IBackgroundJobHandler).Assembly);
        return services;
    }

    [Theory]
    [InlineData(typeof(IBackgroundJobHandler), 8, ServiceLifetime.Scoped)]
    [InlineData(typeof(IRecurringJobProvider), 4, ServiceLifetime.Singleton)]
    [InlineData(typeof(IRuleConditionEvaluator), 4, ServiceLifetime.Singleton)]
    [InlineData(typeof(IFlowActionHandler), 3, ServiceLifetime.Singleton)]
    [InlineData(typeof(IBusinessEventListener), 2, ServiceLifetime.Singleton)]
    [InlineData(typeof(IBusinessEventProvider), 4, ServiceLifetime.Singleton)]
    public void Registers_ExpectedCount_AndLifetime(Type contract, int expectedCount, ServiceLifetime lifetime)
    {
        var descriptors = Register().Where(descriptor => descriptor.ServiceType == contract).ToList();

        Assert.Equal(expectedCount, descriptors.Count);
        Assert.All(descriptors, descriptor => Assert.Equal(lifetime, descriptor.Lifetime));
        Assert.All(descriptors, descriptor => Assert.NotNull(descriptor.ImplementationType));
    }

    [Fact]
    public void Registers_NoDuplicateImplementationPerRole()
    {
        var services = Register();
        Type[] roles =
        [
            typeof(IBackgroundJobHandler), typeof(IRuleConditionEvaluator),
            typeof(IFlowActionHandler), typeof(IBusinessEventListener),
        ];

        foreach (var role in roles)
        {
            var implementations = services
                .Where(descriptor => descriptor.ServiceType == role)
                .Select(descriptor => descriptor.ImplementationType)
                .ToList();

            Assert.Equal(implementations.Count, implementations.Distinct().Count());
        }
    }
}
