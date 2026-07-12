namespace Callora.Host.PluginContracts.Application.Jobs;

/// <summary>
/// Supplies recurring job definitions. Host services register providers in
/// DI; plugins export them via <c>IHostPluginContext.Export</c>.
/// </summary>
public interface IRecurringJobProvider
{
    /// <summary>
    /// Returns the recurring jobs this provider owns.
    /// </summary>
    IReadOnlyList<RecurringJobDefinition> GetDefinitions();
}
