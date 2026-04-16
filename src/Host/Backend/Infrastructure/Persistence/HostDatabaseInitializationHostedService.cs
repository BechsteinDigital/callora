using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class HostDatabaseInitializationHostedService(IServiceProvider services) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HostPersistenceDbContext>();
        await dbContext.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
