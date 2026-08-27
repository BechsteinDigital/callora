using Callora.Host.Cli.Application;

namespace Callora.Core.Tests.Cli;

/// <summary>
/// <c>callora plugin inspect</c> answers "what does this plugin do to the host" before the
/// plugin is anywhere near a host.
/// </summary>
/// <remarks>
/// The runtime already knows all of this — the registry stores hold it once a plugin is
/// installed. What was missing is the answer at the moment it is worth having: before
/// installing, from a file on disk, with no host and no database.
/// </remarks>
public sealed class PluginInspectCliTests
{
    [Fact]
    public async Task Inspect_WithoutAssembly_ExplainsWhatIsMissing()
    {
        var (exitCode, _, stderr) = await RunAsync(["plugin", "inspect"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("--assembly", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inspect_WithMistypedSwitch_RefusesRatherThanIgnores()
    {
        // Ignoring it would silently inspect the manifest beside the assembly and report a
        // plugin that does not exist in that shape.
        var (exitCode, _, stderr) = await RunAsync(
            ["plugin", "inspect", "--assembly", "/x/plugin.dll", "--registy", "/y/registry.json"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("--registy", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inspect_WithMissingAssembly_SaysSoInsteadOfThrowing()
    {
        var (exitCode, _, stderr) = await RunAsync(
            ["plugin", "inspect", "--assembly", Path.Combine(Path.GetTempPath(), "callora-absent.dll")]);

        Assert.Equal(1, exitCode);
        Assert.Contains("callora-absent.dll", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Usage_MentionsInspect()
    {
        // A verb nobody can discover is a verb nobody uses.
        var (_, stdout, _) = await RunAsync(["--help"]);

        Assert.Contains("inspect", stdout, StringComparison.Ordinal);
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = await CalloraCliApplication.RunAsync(
            args, stdout, stderr, Path.GetTempPath(), CancellationToken.None);
        return (exitCode, stdout.ToString(), stderr.ToString());
    }
}

/// <summary>
/// The whole point, against a real scaffolded plugin: manifest facts and what it attaches to,
/// with no host and no database.
/// </summary>
[Collection(ScaffoldedPluginCollection.Name)]
[Trait("Category", "Slow")]
public sealed class PluginInspectAgainstARealPluginTests(ScaffoldedPluginFixture fixture)
{
    [Fact]
    public async Task Inspect_ReportsWhatTheManifestSaysAndWhatTheAssemblyAttachesTo()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await CalloraCliApplication.RunAsync(
            ["plugin", "inspect", "--assembly", fixture.AssemblyPath],
            stdout,
            stderr,
            fixture.RepositoryRoot,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()), stderr.ToString());

        var report = stdout.ToString();
        Assert.Contains("Contract:", report, StringComparison.Ordinal);
        Assert.Contains("Entry type:", report, StringComparison.Ordinal);

        // The part that could not be read from the manifest alone — it comes from the
        // compiled assembly, which is why this command needs both.
        Assert.Contains("IHostManagedPlugin", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inspect_WithoutAManifest_StillReportsWhatItAttachesTo()
    {
        // Inspecting raw build output is a real case, and "no manifest here" is itself the
        // answer rather than a reason to give up.
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await CalloraCliApplication.RunAsync(
            [
                "plugin", "inspect",
                "--assembly", fixture.AssemblyPath,
                "--registry", Path.Combine(Path.GetTempPath(), $"callora-absent-{Guid.NewGuid():N}.json")
            ],
            stdout,
            stderr,
            fixture.RepositoryRoot,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        var report = stdout.ToString();
        Assert.Contains("Manifest:", report, StringComparison.Ordinal);
        Assert.Contains("IHostManagedPlugin", report, StringComparison.Ordinal);
    }
}
