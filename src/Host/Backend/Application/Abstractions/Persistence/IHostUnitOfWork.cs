namespace Callora.Host.Backend.Application.Abstractions.Persistence;

public interface IHostUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
