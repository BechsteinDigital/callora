namespace Callora.Core.Tests.Cli;

[CollectionDefinition(Name)]
public sealed class ScaffoldedPluginCollection : ICollectionFixture<ScaffoldedPluginFixture>
{
    public const string Name = "scaffolded-plugin";
}
