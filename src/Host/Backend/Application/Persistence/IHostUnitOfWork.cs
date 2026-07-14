namespace Callora.Host.Backend.Application.Persistence;

public interface IHostUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
