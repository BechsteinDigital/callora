using Callora.Core.Application.Cli;
using Callora.Core.Application.Cli.Commands;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Plugins;
using Callora.Core.Domain.Plugins;
using Callora.Core.Tests.Support;

namespace Callora.Core.Tests.Application.Cli;

public sealed class CalloraConsoleRunnerTests
{
    [Fact]
    public async Task RunAsync_DispatchesToNamedCommand_WithRemainingArgs()
    {
        var command = new RecordingCommand("plugin:refresh");
        var runner = new CalloraConsoleRunner([command], EmptyCatalog());
        var output = new StringWriter();

        var exit = await runner.RunAsync(["plugin:refresh", "--force"], output);

        Assert.Equal(0, exit);
        Assert.Equal(["--force"], command.ReceivedArgs);
    }

    [Fact]
    public async Task RunAsync_UnknownCommand_ReturnsErrorAndListsCommands()
    {
        var runner = new CalloraConsoleRunner([new RecordingCommand("plugin:refresh")], EmptyCatalog());
        var output = new StringWriter();

        var exit = await runner.RunAsync(["nope"], output);

        Assert.Equal(1, exit);
        Assert.Contains("Unknown command 'nope'", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("plugin:refresh", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_NoArgs_ListsCommands()
    {
        var runner = new CalloraConsoleRunner([new RecordingCommand("plugin:list")], EmptyCatalog());
        var output = new StringWriter();

        var exit = await runner.RunAsync([], output);

        Assert.Equal(0, exit);
        Assert.Contains("plugin:list", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PluginRefreshCommand_PrintsReconciliationSummary()
    {
        var discovery = new StubDiscovery(new PluginDiscoveryRefreshResult(["alpha"], [], ["beta"], []));
        var output = new StringWriter();

        var exit = await new PluginRefreshCommand(discovery).ExecuteAsync([], output);

        Assert.Equal(0, exit);
        Assert.Contains("1 added", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("added: alpha", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("removed: beta", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PluginListCommand_ListsInstalledPlugins()
    {
        var lifecycle = new RecordingPluginLifecycleService();
        lifecycle.Installations.Add(new PluginInstallationSnapshot(
            "voip", "Voip", "/tmp/voip.dll", "Voip.Entry", (int)PluginInstallationState.Active,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var output = new StringWriter();

        var exit = await new PluginListCommand(lifecycle).ExecuteAsync([], output);

        Assert.Equal(0, exit);
        Assert.Contains("voip", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Active", output.ToString(), StringComparison.Ordinal);
    }

    private static ICalloraPluginCatalog EmptyCatalog()
        => new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>());

    private sealed class RecordingCommand(string name) : ICalloraConsoleCommand
    {
        public string Name => name;

        public string Description => "test command";

        public IReadOnlyList<string>? ReceivedArgs { get; private set; }

        public Task<int> ExecuteAsync(IReadOnlyList<string> args, TextWriter output, CancellationToken cancellationToken = default)
        {
            ReceivedArgs = args;
            return Task.FromResult(0);
        }
    }

    private sealed class StubDiscovery(PluginDiscoveryRefreshResult result) : IPluginDiscoveryService
    {
        public Task<PluginDiscoveryRefreshResult> RefreshAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }
}
