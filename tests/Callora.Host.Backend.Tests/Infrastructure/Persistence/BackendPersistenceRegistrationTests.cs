using Callora.Host.Backend.Application.Policies;
using Callora.Host.Backend.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Callora.Host.Backend.Tests.Infrastructure.Persistence;

public sealed class BackendPersistenceRegistrationTests
{
    [Fact]
    public void DbContext_ResolvesWithWorkspaceScope_WithoutCaptiveDependency()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendPersistence(new BackendHostOptions
        {
            DatabaseConnectionString = "Host=localhost;Database=x;Username=u;Password=p"
        });

        // ValidateScopes catches a scoped service captured by a singleton — the
        // risk introduced by giving the DbContext an IWorkspaceScopeContext.
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HostPersistenceDbContext>();

        Assert.NotNull(context);
    }
}
