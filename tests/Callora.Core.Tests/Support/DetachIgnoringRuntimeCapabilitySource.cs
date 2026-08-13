using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Eine Capability-Quelle, die ihren Abonnenten beim Abhängen behält.
/// </summary>
/// <remarks>
/// <para>
/// Damit lässt sich ohne Zeitsteuerung genau der Fall auslösen, der nebenläufig entsteht: Der Vertrag
/// erlaubt <see cref="IRuntimeCapabilitySource.CapabilitiesChanged"/> aus jedem Thread, also kann ein
/// Aufruf beim <c>Unregister</c> bereits unterwegs sein und die Registry erst danach erreichen — das
/// <c>-=</c> kommt für ihn zu spät. Eine Quelle, die das <c>-=</c> ignoriert, erzeugt dieselbe
/// Reihenfolge deterministisch.
/// </para>
/// <para>
/// Sie ist außerdem nicht nur ein Testmittel: Eine Quelle mit selbst geschriebenen Ereignis-Accessoren
/// darf sich so verhalten, und der Host darf ihr dann trotzdem nicht glauben.
/// </para>
/// </remarks>
public sealed class DetachIgnoringRuntimeCapabilitySource : IRuntimeCapabilitySource
{
    private readonly List<RuntimeCapabilityGrant> _grants;
    private Action<RuntimeCapabilityChanged>? _handler;

    public DetachIgnoringRuntimeCapabilitySource(params RuntimeCapabilityGrant[] initialGrants) =>
        _grants = [.. initialGrants];

    public IReadOnlyCollection<RuntimeCapabilityGrant> CurrentGrants => _grants;

    public event Action<RuntimeCapabilityChanged>? CapabilitiesChanged
    {
        add => _handler = value;
        remove { /* Bewusst folgenlos — das ist der Punkt der Attrappe. */ }
    }

    /// <summary>Feuert an den Abonnenten, den die Registry für abgehängt hält.</summary>
    public void Raise(string capability, string? workspaceKey, bool satisfied) =>
        _handler?.Invoke(new RuntimeCapabilityChanged(capability, workspaceKey, satisfied));
}
