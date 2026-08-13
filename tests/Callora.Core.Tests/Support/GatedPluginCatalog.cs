using Callora.Core.Application.Http.Contracts;
using Callora.Core.Application.Plugins;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Ein Katalog, dessen Lesevorgang sich anhalten lässt — um zwei überlappende Neubauten in eine
/// bestimmte Reihenfolge zu zwingen, ohne auf Zeit zu warten.
/// </summary>
/// <remarks>
/// Nebenläufigkeit mit <c>Thread.Sleep</c> zu treffen ergibt einen Test, der auf einem ausgelasteten
/// Rechner mal so und mal anders ausgeht. Hier hält der erste Leser an einem Tor, bis der zweite
/// fertig ist; danach läuft er mit seiner veralteten Sicht weiter. Das ist genau die Verschränkung,
/// die in Produktion selten passiert und dann nicht erklärbar ist.
/// </remarks>
public sealed class GatedPluginCatalog : ICalloraPluginCatalog
{
    private const string OwningPluginId = "test-plugin";
    private readonly ManualResetEventSlim _released = new(false);
    private readonly ManualResetEventSlim _arrived = new(false);
    private object[] _controllers = [];
    private int _reads;

    public void SetExports(params object[] controllers) => _controllers = controllers;

    /// <summary>Der nächste Lesevorgang hält an, bis <see cref="Release"/> gerufen wird.</summary>
    public void GateNextRead() => _released.Reset();

    /// <summary>Wartet, bis ein Leser am Tor angekommen ist.</summary>
    public void WaitUntilGated() => _arrived.Wait(TimeSpan.FromSeconds(10));

    /// <summary>Lässt den wartenden Leser weiterlaufen.</summary>
    public void Release() => _released.Set();

    public bool TryGetExport(Type contractType, out object? service)
    {
        service = contractType == typeof(IApiController) ? _controllers.FirstOrDefault() : null;
        return service is not null;
    }

    public IReadOnlyList<object> GetExports(Type contractType) =>
        contractType == typeof(IApiController) ? _controllers : [];

    public IReadOnlyList<CalloraPluginExport> GetOwnedExports(Type contractType)
    {
        if (contractType != typeof(IApiController))
        {
            return [];
        }

        // Die Momentaufnahme entsteht VOR dem Warten — das ist der Punkt: Der angehaltene Leser
        // trägt danach eine Sicht mit sich, die die Welt längst überholt hat.
        var snapshot = _controllers
            .Select(controller => new CalloraPluginExport(OwningPluginId, controller))
            .ToArray();

        if (Interlocked.Increment(ref _reads) == 1)
        {
            _arrived.Set();
            _released.Wait(TimeSpan.FromSeconds(10));
        }

        return snapshot;
    }
}
