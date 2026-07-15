using Callora.Core.Application.Jobs.Contracts;

namespace Callora.Core.Tests.Support;

internal sealed class StaticRecurringJobProvider(params RecurringJobDefinition[] definitions) : IRecurringJobProvider
{
    public IReadOnlyList<RecurringJobDefinition> GetDefinitions() => definitions;
}
