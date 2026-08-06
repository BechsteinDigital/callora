using Callora.Core.Extensibility;

namespace Callora.Core.Application.Surfaces.SharedContext;

/// <summary>
/// Declares the shared context keys a plugin publishes. Declaration is a precondition, not
/// documentation: a key nobody declared cannot be published, and a field nobody declared is not
/// delivered even if a publisher sets it.
/// <para>
/// That is deliberate friction. Shared context leaves the surface it was produced on and carries
/// personal data; the cost of describing it once is small against the cost of shipping a field
/// nobody could name a purpose for.
/// </para>
/// </summary>
[CalloraExtensible("Extension point — implement to declare shared surface context keys")]
public interface ISharedContextKeyContributor
{
    /// <summary>The keys this plugin publishes. Read once at composition.</summary>
    IReadOnlyList<SharedContextKeyDeclaration> SharedContextKeys { get; }
}
