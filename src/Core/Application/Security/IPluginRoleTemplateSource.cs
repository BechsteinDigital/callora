using Callora.Core.Extensibility;

namespace Callora.Core.Application.Security;

/// <summary>
/// Welche Rollen die installierten Plugins nach sich ziehen.
/// </summary>
[CalloraInternal("Rollenbereitstellung — Durchsetzung, kein Plugin-Vertrag (REV2 §7.2)")]
public interface IPluginRoleTemplateSource
{
    /// <summary>Eine Vorlage je Plugin, das überhaupt Berechtigungen mitbringt.</summary>
    Task<IReadOnlyList<PluginRoleTemplate>> ListAsync(CancellationToken cancellationToken = default);
}
