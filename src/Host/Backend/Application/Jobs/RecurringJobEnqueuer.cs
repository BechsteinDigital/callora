using Callora.Host.Backend.Application.Abstractions.Jobs;
using Callora.Host.Backend.Domain.Jobs;
using Callora.Host.PluginContracts.Application.Jobs;
using Callora.Hosting.Application.Plugins;

namespace Callora.Host.Backend.Application.Jobs;

/// <summary>
/// Enqueues fixed-interval recurring jobs from host providers and plugin
/// exports. Skips a cycle while a job of the same type is still active.
/// </summary>
public sealed class RecurringJobEnqueuer(
    IEnumerable<IRecurringJobProvider> hostProviders,
    ICalloraPluginCatalog pluginCatalog)
{
    private readonly Dictionary<string, DateTimeOffset> _lastEnqueuedByType = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncLock = new();

    /// <summary>
    /// Enqueues every due recurring job. The first run happens one interval
    /// after startup to avoid boot storms.
    /// </summary>
    public async Task EnqueueDueJobsAsync(
        IBackgroundJobStore jobStore,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(jobStore);

        foreach (var definition in CollectDefinitions())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryMarkDue(definition, nowUtc))
                continue;

            if (await jobStore.HasActiveJobAsync(definition.JobType, cancellationToken).ConfigureAwait(false))
                continue;

            var job = BackgroundJob.Create(
                definition.JobType,
                definition.PayloadJson,
                scheduledAtUtc: nowUtc,
                maxAttempts: definition.MaxAttempts,
                workspaceKey: definition.WorkspaceKey,
                nowUtc: nowUtc);

            await jobStore.AddAsync(job, cancellationToken).ConfigureAwait(false);
        }
    }

    private IReadOnlyList<RecurringJobDefinition> CollectDefinitions() =>
        hostProviders
            .Concat(pluginCatalog.GetExports<IRecurringJobProvider>())
            .SelectMany(static provider => provider.GetDefinitions())
            .Where(static definition => definition.Interval > TimeSpan.Zero)
            .ToArray();

    private bool TryMarkDue(RecurringJobDefinition definition, DateTimeOffset nowUtc)
    {
        lock (_syncLock)
        {
            if (_lastEnqueuedByType.TryGetValue(definition.JobType, out var lastEnqueued) &&
                nowUtc - lastEnqueued < definition.Interval)
            {
                return false;
            }

            if (!_lastEnqueuedByType.ContainsKey(definition.JobType))
            {
                // Erste Sichtung: Startzeit merken, erster Lauf nach einem Intervall.
                _lastEnqueuedByType[definition.JobType] = nowUtc;
                return false;
            }

            _lastEnqueuedByType[definition.JobType] = nowUtc;
            return true;
        }
    }
}
