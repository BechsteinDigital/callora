using Callora.Host.Backend.Application.Abstractions.Persistence;

namespace Callora.Host.Backend.Tests.Support;

public sealed class NoOpHostUnitOfWork : IHostUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(1);
}
