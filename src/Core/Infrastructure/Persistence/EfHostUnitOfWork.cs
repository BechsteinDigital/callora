using Callora.Core.Application.Persistence;

namespace Callora.Core.Infrastructure.Persistence;

public sealed class EfHostUnitOfWork(HostPersistenceDbContext dbContext) : IHostUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
