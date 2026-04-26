using System.Collections.Concurrent;

namespace Callora.Plugins.Voip.Application.Admin;

public sealed class InMemorySipAccountStore : ISipAccountStore
{
    private readonly ConcurrentDictionary<string, SipAccountEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public InMemorySipAccountStore()
    {
        var now = DateTimeOffset.UtcNow;
        var seed = new SipAccountEntry(
            SipAccountId: "sip-main",
            Username: "main",
            Domain: "voice.callora.local",
            DisplayName: "Main SIP Account",
            Secret: "change-me",
            IsActive: true,
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
        _entries[seed.SipAccountId] = seed;
    }

    public IReadOnlyList<SipAccountEntry> List()
    {
        return _entries.Values
            .OrderBy(entry => entry.SipAccountId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public SipAccountEntry? Get(string sipAccountId)
    {
        if (string.IsNullOrWhiteSpace(sipAccountId))
            return null;

        return _entries.TryGetValue(sipAccountId.Trim(), out var entry)
            ? entry
            : null;
    }

    public SipAccountEntry Create(UpsertSipAccountRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = DateTimeOffset.UtcNow;
        var id = BuildSipAccountId(request.Username, request.Domain);

        var entry = new SipAccountEntry(
            SipAccountId: id,
            Username: request.Username.Trim(),
            Domain: request.Domain.Trim(),
            DisplayName: request.DisplayName.Trim(),
            Secret: request.Secret,
            IsActive: request.IsActive,
            CreatedAtUtc: now,
            UpdatedAtUtc: now);

        if (!_entries.TryAdd(entry.SipAccountId, entry))
        {
            throw new InvalidOperationException($"SIP account '{entry.SipAccountId}' already exists.");
        }

        return entry;
    }

    public SipAccountEntry? Update(string sipAccountId, UpsertSipAccountRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(sipAccountId))
            return null;

        var normalizedId = sipAccountId.Trim();
        while (true)
        {
            if (!_entries.TryGetValue(normalizedId, out var current))
                return null;

            var updated = current with
            {
                Username = request.Username.Trim(),
                Domain = request.Domain.Trim(),
                DisplayName = request.DisplayName.Trim(),
                Secret = request.Secret,
                IsActive = request.IsActive,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            if (_entries.TryUpdate(normalizedId, updated, current))
                return updated;
        }
    }

    public bool Delete(string sipAccountId)
    {
        if (string.IsNullOrWhiteSpace(sipAccountId))
            return false;

        return _entries.TryRemove(sipAccountId.Trim(), out _);
    }

    private static string BuildSipAccountId(string username, string domain)
    {
        var normalizedUsername = username.Trim().ToLowerInvariant();
        var normalizedDomain = domain.Trim().ToLowerInvariant();

        var joined = $"{normalizedUsername}@{normalizedDomain}";
        var safeChars = joined
            .Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.' || ch == '@' ? ch : '-')
            .ToArray();
        return new string(safeChars);
    }
}
