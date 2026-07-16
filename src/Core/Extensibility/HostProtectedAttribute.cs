namespace Callora.Core.Extensibility;

/// <summary>
/// Marks a host contributor (job handler, flow action) as protected from plugin
/// override. Under plugin-wins resolution (R1) a plugin export of the same key
/// normally replaces the host contributor; a host contributor marked
/// <see cref="HostProtectedAttribute"/> keeps precedence instead. For
/// security-/compliance-critical host handlers — e.g. GDPR retention purge or
/// entitlement sync — that must not be silently supplanted by a plugin.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
internal sealed class HostProtectedAttribute : Attribute;
