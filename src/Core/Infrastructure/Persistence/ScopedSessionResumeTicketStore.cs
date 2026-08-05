using Callora.Core.Application.Plugins;
using Callora.Core.Domain.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Callora.Core.Infrastructure.Persistence;

/// <summary>
/// Singleton facade over the scoped EF-backed resume-ticket store. Creates one service scope per
/// operation so a plugin can hold this from the root provider without capturing a scoped DbContext.
/// </summary>
/// <remarks>
/// A plugin issues and redeems tickets from wherever its session lives — a WebSocket handler, a
/// connect authorizer — none of which run inside a request scope it could borrow.
/// </remarks>
public sealed class ScopedSessionResumeTicketStore(IServiceScopeFactory scopeFactory) : ISessionResumeTicketStore
{
    public async Task CreateAsync(
        SessionResumeTicketRecord record,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        await ResolveInner(scope).CreateAsync(record, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SessionResumeTicketRecord?> ConsumeAsync(
        string tokenHash,
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        return await ResolveInner(scope)
            .ConsumeAsync(tokenHash, pluginId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(
        string tokenHash,
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        return await ResolveInner(scope)
            .DeleteAsync(tokenHash, pluginId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> PurgeExpiredAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        return await ResolveInner(scope).PurgeExpiredAsync(nowUtc, cancellationToken).ConfigureAwait(false);
    }

    private static EfSessionResumeTicketStore ResolveInner(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<EfSessionResumeTicketStore>();
}
