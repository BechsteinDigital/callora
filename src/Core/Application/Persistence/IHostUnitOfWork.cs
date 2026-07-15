namespace Callora.Core.Application.Persistence;

public interface IHostUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
