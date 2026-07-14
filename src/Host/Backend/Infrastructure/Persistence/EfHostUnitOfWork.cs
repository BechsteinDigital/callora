using Callora.Host.Backend.Application.Persistence;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class EfHostUnitOfWork(HostPersistenceDbContext dbContext) : IHostUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
