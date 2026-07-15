using Callora.Host.PluginContracts.Application.Jobs;

namespace Callora.Core.Tests.Support;

internal sealed class StaticRecurringJobProvider(params RecurringJobDefinition[] definitions) : IRecurringJobProvider
{
    public IReadOnlyList<RecurringJobDefinition> GetDefinitions() => definitions;
}
