using Callora.Core.Application.Persistence;

namespace Callora.Core.Tests.Support;

public sealed class NoOpHostUnitOfWork : IHostUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(1);
}
