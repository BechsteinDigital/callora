using Callora.Core.Application.Audit;
using Callora.Core.Application.Policies;

namespace Callora.Core.Tests.Application.Audit;

public sealed class FileHostAuditStoreThreadSafetyTests
{
    [Fact]
    public async Task AppendAsync_ConcurrentWrites_ProducesConsistentEntries()
    {
        var auditPath = Path.Combine(Path.GetTempPath(), $"callora-audit-{Guid.NewGuid():N}.jsonl");
        var options = new BackendHostOptions { AuditFilePath = auditPath };
        var sut = new FileHostAuditStore(options);

        var writes = Enumerable.Range(0, 64)
            .Select(i => sut.AppendAsync(
                new HostAuditEntry(
                    DateTimeOffset.UtcNow,
                    "plugin.activate",
                    $"plugin-{i}",
                    true,
                    "test",
                    null)))
            .ToArray();

        await Task.WhenAll(writes);

        var recent = await sut.GetRecentAsync(1000);
        Assert.Equal(64, recent.Count);

        var pluginIds = recent
            .Select(static entry => entry.PluginId)
            .Where(static id => id is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(64, pluginIds.Count);

        if (File.Exists(auditPath))
        {
            File.Delete(auditPath);
        }
    }
}
