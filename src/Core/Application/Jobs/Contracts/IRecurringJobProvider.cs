using Callora.Core.Extensibility;

namespace Callora.Core.Application.Jobs.Contracts;

/// <summary>
/// Supplies recurring job definitions. Host services register providers in
/// DI; plugins export them via <c>IHostPluginContext.Export</c>.
/// </summary>
[CalloraExtensible("Extension point — implement and export to supply recurring jobs (REV2 §8.2)")]
public interface IRecurringJobProvider
{
    /// <summary>
    /// Returns the recurring jobs this provider owns.
    /// </summary>
    IReadOnlyList<RecurringJobDefinition> GetDefinitions();
}
