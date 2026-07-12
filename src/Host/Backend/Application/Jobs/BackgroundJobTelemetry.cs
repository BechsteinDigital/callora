using System.Diagnostics.Metrics;

namespace Callora.Host.Backend.Application.Jobs;

/// <summary>
/// Metrics for the background job pipeline.
/// </summary>
public static class BackgroundJobTelemetry
{
    public const string MeterName = "Callora.Host.Backend.BackgroundJobs";

    private static readonly Meter JobMeter = new(MeterName);

    private static readonly Counter<long> ProcessedCounter = JobMeter.CreateCounter<long>(
        "callora.jobs.processed",
        description: "Number of processed background job attempts by outcome.");

    private static readonly Histogram<double> DurationMs = JobMeter.CreateHistogram<double>(
        "callora.jobs.duration",
        unit: "ms",
        description: "Duration of background job attempts.");

    /// <summary>
    /// Records one completed job attempt.
    /// </summary>
    public static void RecordAttempt(string jobType, string outcome, double durationMs)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("job.type", jobType),
            new("job.outcome", outcome)
        };

        ProcessedCounter.Add(1, tags);
        DurationMs.Record(durationMs, tags);
    }
}
