using Callora.Hosting.Infrastructure.DependencyInjection;

namespace Callora.Host.Backend.Infrastructure.DependencyInjection;

internal sealed class ServiceCollectionHostRegistry(IServiceCollection services) : IHostServiceRegistry
{
    public void AddSingleton(Type serviceType, Type implementationType) =>
        services.AddSingleton(serviceType, implementationType);

    public void AddSingleton(Type serviceType, object instance) =>
        services.AddSingleton(serviceType, instance);

    public void AddSingleton(Type serviceType, Func<IServiceProvider, object> implementationFactory) =>
        services.AddSingleton(serviceType, implementationFactory);
}
