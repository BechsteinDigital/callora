using Callora.Host.PluginContracts.Application.Persistence;
using Callora.Host.PluginContracts.Application.Secrets;
using Callora.Plugin.Communication.Application.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Callora.Plugin.Communication.Application.Accounts;

/// <summary>
/// SIP account store backed by the plugin's own EF Core database (PLAT-260):
/// real typed rows in the plugin_communication schema instead of jsonb documents. The
/// secret is encrypted at rest via the host data protector; legacy plaintext
/// stays readable and is re-encrypted on the next write.
/// </summary>
public sealed class EfSipAccountStore(
    IPluginDbContextFactory<VoipDbContext> dbContextFactory,
    IPluginDataProtector dataProtector) : ISipAccountStore
{
    public async Task<IReadOnlyList<SipAccountEntry>> ListAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        var key = workspaceKey.Trim();
        await using var db = dbContextFactory.CreateDbContext();
        var rows = await db.SipAccounts
            .AsNoTracking()
            .Where(x => x.WorkspaceKey == key)
            .OrderBy(x => x.SipAccountId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(ToEntry).ToArray();
    }

    public async Task<SipAccountEntry?> GetAsync(
        string workspaceKey,
        string sipAccountId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sipAccountId))
            return null;

        await using var db = dbContextFactory.CreateDbContext();
        var row = await FindAsync(db, workspaceKey, sipAccountId.Trim(), cancellationToken).ConfigureAwait(false);
        return row is null ? null : ToEntry(row);
    }

    public async Task<SipAccountEntry> CreateAsync(
        string workspaceKey,
        UpsertSipAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var key = workspaceKey.Trim();
        var now = DateTimeOffset.UtcNow;
        var id = SipAccountIdFactory.Create(request.Username, request.Domain);

        await using var db = dbContextFactory.CreateDbContext();
        if (await FindAsync(db, key, id, cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new InvalidOperationException($"SIP account '{id}' already exists.");
        }

        var row = new SipAccount
        {
            WorkspaceKey = key,
            SipAccountId = id,
            Username = request.Username.Trim(),
            Domain = request.Domain.Trim(),
            DisplayName = request.DisplayName.Trim(),
            ProtectedSecret = dataProtector.Protect(CommunicationPlugin.Id, request.Secret),
            IsActive = request.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.SipAccounts.Add(row);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToEntry(row);
    }

    public async Task<SipAccountEntry?> UpdateAsync(
        string workspaceKey,
        string sipAccountId,
        UpsertSipAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var db = dbContextFactory.CreateDbContext();
        var row = await FindAsync(db, workspaceKey, sipAccountId.Trim(), cancellationToken).ConfigureAwait(false);
        if (row is null)
            return null;

        row.Username = request.Username.Trim();
        row.Domain = request.Domain.Trim();
        row.DisplayName = request.DisplayName.Trim();
        row.ProtectedSecret = dataProtector.Protect(CommunicationPlugin.Id, request.Secret);
        row.IsActive = request.IsActive;
        row.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToEntry(row);
    }

    public async Task<bool> DeleteAsync(
        string workspaceKey,
        string sipAccountId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sipAccountId))
            return false;

        var key = workspaceKey.Trim();
        var id = sipAccountId.Trim();
        await using var db = dbContextFactory.CreateDbContext();
        var deleted = await db.SipAccounts
            .Where(x => x.WorkspaceKey == key && x.SipAccountId == id)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        return deleted > 0;
    }

    public async Task<IReadOnlyList<string>> ListWorkspaceKeysAsync(CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory.CreateDbContext();
        return await db.SipAccounts
            .AsNoTracking()
            .Select(x => x.WorkspaceKey)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static Task<SipAccount?> FindAsync(
        VoipDbContext db,
        string workspaceKey,
        string sipAccountId,
        CancellationToken cancellationToken)
    {
        var key = workspaceKey.Trim();
        return db.SipAccounts.FirstOrDefaultAsync(
            x => x.WorkspaceKey == key && x.SipAccountId == sipAccountId,
            cancellationToken);
    }

    private SipAccountEntry ToEntry(SipAccount row) => new(
        row.SipAccountId,
        row.Username,
        row.Domain,
        row.DisplayName,
        UnprotectSecret(row.ProtectedSecret),
        row.IsActive,
        row.CreatedAtUtc,
        row.UpdatedAtUtc);

    private string UnprotectSecret(string storedSecret) =>
        dataProtector.TryUnprotect(CommunicationPlugin.Id, storedSecret, out var plaintext)
            ? plaintext
            : storedSecret;
}
