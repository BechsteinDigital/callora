using Callora.Core.Application.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace Callora.Core.Infrastructure.Diagnostics;

/// <summary>
/// Feeds every executed database command to <see cref="PluginExecutionRecorder"/>, attributed
/// to whichever plugin's code was running.
/// </summary>
/// <remarks>
/// <para>
/// Registered on the host context and on every plugin context, because a plugin's own
/// <c>DbContext</c> is configured in one place — <c>NpgsqlPluginDbContextProvider</c> — and
/// its queries would otherwise be invisible to a host-side interceptor.
/// </para>
/// <para>
/// The cost while switched off is one field read and an early return in the recorder. That
/// matters more than it sounds: this runs on every query of every request for the entire
/// life of the host, and a diagnostic that is expensive when idle is one nobody leaves
/// installed.
/// </para>
/// </remarks>
public sealed class RecordingDbCommandInterceptor(PluginExecutionRecorder recorder) : DbCommandInterceptor
{
    /// <inheritdoc />
    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        CaptureFrom(command, eventData);
        return base.ReaderExecuted(command, eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        CaptureFrom(command, eventData);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        CaptureFrom(command, eventData);
        return base.NonQueryExecuted(command, eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        CaptureFrom(command, eventData);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        CaptureFrom(command, eventData);
        return base.ScalarExecuted(command, eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        CaptureFrom(command, eventData);
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    /// The one decision this interceptor makes, kept separate so it can be exercised without
    /// standing up EF Core's event plumbing.
    /// </summary>
    internal void Capture(string commandText, TimeSpan duration) =>
        recorder.RecordCommand(PluginExecutionScope.Current, commandText, duration);

    private void CaptureFrom(DbCommand command, CommandExecutedEventData eventData) =>
        Capture(command.CommandText, eventData.Duration);
}
