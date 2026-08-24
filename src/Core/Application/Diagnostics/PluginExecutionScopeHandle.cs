namespace Callora.Core.Application.Diagnostics;

/// <summary>
/// Undoes one <see cref="PluginExecutionScope.Enter"/>.
/// </summary>
/// <remarks>
/// Restores the previous value rather than clearing it: a plugin calling into another
/// plugin's exported service must not leave the first one credited with the second's
/// queries once the inner call returns.
/// </remarks>
internal sealed class PluginExecutionScopeHandle(string? previous) : IDisposable
{
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        PluginExecutionScope.Restore(previous);
    }
}
