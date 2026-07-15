using Callora.Host.PluginContracts.Application.Jobs;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Job handler fake: records executions and optionally fails a configurable
/// number of times before succeeding.
/// </summary>
public sealed class RecordingBackgroundJobHandler(string jobType, int failuresBeforeSuccess = 0) : IBackgroundJobHandler
{
    private readonly List<BackgroundJobExecutionContext> _executions = [];
    private int _remainingFailures = failuresBeforeSuccess;

    public string JobType { get; } = jobType;

    public IReadOnlyList<BackgroundJobExecutionContext> Executions => _executions;

    public Task ExecuteAsync(BackgroundJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        _executions.Add(context);
        if (_remainingFailures > 0)
        {
            _remainingFailures--;
            throw new InvalidOperationException("Simulated job failure.");
        }

        return Task.CompletedTask;
    }
}
