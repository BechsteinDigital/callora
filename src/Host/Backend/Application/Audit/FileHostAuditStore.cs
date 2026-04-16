using System.Text.Json;
using Callora.Host.Backend.Application.Abstractions;
using Callora.Host.Backend.Application.Policies;

namespace Callora.Host.Backend.Application.Audit;

public sealed class FileHostAuditStore(BackendHostOptions options) : IHostAuditStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly SemaphoreSlim _ioLock = new(1, 1);

    public async Task AppendAsync(HostAuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        var path = options.AuditFilePath;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(entry, JsonOptions);
        await _ioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(path, json + Environment.NewLine, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task<IReadOnlyList<HostAuditEntry>> GetRecentAsync(int take = 200, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        take = Math.Clamp(take, 1, 2_000);

        var path = options.AuditFilePath;
        if (!File.Exists(path))
            return [];

        await _ioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tail = new Queue<string>(take);
            foreach (var line in File.ReadLines(path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (tail.Count == take)
                    tail.Dequeue();
                tail.Enqueue(line);
            }

            var parsed = new List<HostAuditEntry>(tail.Count);
            foreach (var line in tail.Reverse())
            {
                var entry = JsonSerializer.Deserialize<HostAuditEntry>(line, JsonOptions);
                if (entry is not null)
                    parsed.Add(entry);
            }

            return parsed;
        }
        finally
        {
            _ioLock.Release();
        }
    }
}
