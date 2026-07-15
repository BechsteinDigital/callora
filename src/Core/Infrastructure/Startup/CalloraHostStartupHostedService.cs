using Callora.Core.Application.Startup;

namespace Callora.Core.Infrastructure.Startup;

public sealed class CalloraHostStartupHostedService(
    IServiceProvider services,
    CalloraHostStartup startup) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        startup.StartAsync(services, cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
