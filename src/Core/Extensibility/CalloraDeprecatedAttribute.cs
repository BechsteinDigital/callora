namespace Callora.Core.Extensibility;

/// <summary>
/// Marks an extension-surface type or member as deprecated: still working, already on its
/// way out. Consuming it from outside the framework assemblies is reported as
/// <c>CAL0005</c> — a warning in the plugin author's own build, in their own repository, at
/// their own pace.
/// </summary>
/// <remarks>
/// <para>
/// Exists because the contract had exactly two states: it breaks (<c>contractVersion++</c>,
/// every external plugin must be rebuilt) or it does not. There was no way to say "this
/// still works, will warn from now on, and is gone in v3", so every change had to be argued
/// as one or the other — and the pressure was always toward "additive, ship it".
/// </para>
/// <para>
/// The rung this marks is deliberately non-breaking. Moving a member here is an additive
/// change: refresh <c>src/Core/ExtensionSurface.txt</c> and nothing else. Removing it
/// afterwards is not, and the surface gate refuses that without a contract-version bump —
/// which is the whole point of announcing the removal first.
/// </para>
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface |
    AttributeTargets.Enum | AttributeTargets.Delegate | AttributeTargets.Method |
    AttributeTargets.Constructor | AttributeTargets.Property | AttributeTargets.Field |
    AttributeTargets.Event,
    Inherited = false,
    AllowMultiple = false)]
public sealed class CalloraDeprecatedAttribute : Attribute
{
    /// <summary>Marks the target as deprecated, recording when and until when.</summary>
    /// <param name="since">
    /// Platform version in which the deprecation was announced, e.g. <c>"0.9.2"</c>.
    /// Recorded so a plugin author can tell a fresh deprecation from a long-standing one.
    /// </param>
    /// <param name="errorsIn">
    /// Contract version in which the member stops working, e.g. <c>"v3"</c>. This is a
    /// promise: the member survives every release until that contract version.
    /// </param>
    public CalloraDeprecatedAttribute(string since, string errorsIn)
    {
        Since = since;
        ErrorsIn = errorsIn;
    }

    /// <summary>Platform version in which the deprecation was announced.</summary>
    public string Since { get; }

    /// <summary>Contract version in which the member stops working.</summary>
    public string ErrorsIn { get; }

    /// <summary>
    /// What to use instead, surfaced verbatim in the CAL0005 message.
    /// </summary>
    /// <remarks>
    /// A deprecation without a replacement tells a plugin author their code is doomed and
    /// not what to do about it, which is how a warning becomes noise someone suppresses.
    /// </remarks>
    public string? Replacement { get; init; }
}
