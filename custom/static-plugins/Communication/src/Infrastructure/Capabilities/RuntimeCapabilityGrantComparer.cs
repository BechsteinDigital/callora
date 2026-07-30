using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Plugin.Communication.Infrastructure.Capabilities;

internal sealed class RuntimeCapabilityGrantComparer : IEqualityComparer<RuntimeCapabilityGrant>
{
    public static RuntimeCapabilityGrantComparer Instance { get; } = new();

    public bool Equals(RuntimeCapabilityGrant? x, RuntimeCapabilityGrant? y) =>
        ReferenceEquals(x, y)
        || x is not null
        && y is not null
        && string.Equals(x.Capability, y.Capability, StringComparison.OrdinalIgnoreCase)
        && string.Equals(x.WorkspaceKey, y.WorkspaceKey, StringComparison.OrdinalIgnoreCase);

    public int GetHashCode(RuntimeCapabilityGrant obj) =>
        HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Capability),
            obj.WorkspaceKey is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(obj.WorkspaceKey));
}
