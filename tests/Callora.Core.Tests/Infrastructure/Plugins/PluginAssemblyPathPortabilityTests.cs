using Callora.Core.Application.Options;
using Callora.Core.Infrastructure.Plugins;

namespace Callora.Core.Tests.Infrastructure.Plugins;

public sealed class PluginAssemblyPathPortabilityTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "callora-portability"));
    private static readonly string PluginRoot = Path.Combine(Root, "custom", "plugins");
    private static readonly string StaticPluginRoot = Path.Combine(Root, "custom", "static-plugins");

    private static PluginAssemblyPathPortability CreateSut() => new(new CalloraHostingOptions
    {
        PluginDirectory = PluginRoot,
        StaticPluginDirectory = StaticPluginRoot,
    });

    [Fact]
    public void ToStoredPath_UnderPluginDirectory_ReplacesTheRootWithItsToken()
    {
        var stored = CreateSut().ToStoredPath(Path.Combine(PluginRoot, "videoconference", "Callora.Plugin.VideoConference.dll"));

        Assert.Equal("${PluginDirectory}/videoconference/Callora.Plugin.VideoConference.dll", stored);
    }

    [Fact]
    public void ToStoredPath_UnderStaticPluginDirectory_ReplacesTheRootWithItsToken()
    {
        var stored = CreateSut().ToStoredPath(Path.Combine(StaticPluginRoot, "Communication", "Callora.Plugin.Communication.dll"));

        Assert.Equal("${StaticPluginDirectory}/Communication/Callora.Plugin.Communication.dll", stored);
    }

    // Ein per NuGet oder von Hand installiertes Plugin liegt unter keiner der beiden Wurzeln.
    // Für das gibt es keine bekannte Bezugsgröße, und ein absoluter Pfad ist dort die richtige
    // Antwort — die Unterscheidung ist der Punkt, nicht die Abschaffung absoluter Pfade.
    [Fact]
    public void ToStoredPath_OutsideBothRoots_KeepsTheAbsolutePath()
    {
        var outside = Path.Combine(Root, "opt", "nuget", "Callora.Plugin.Foreign.dll");

        Assert.Equal(outside, CreateSut().ToStoredPath(outside));
    }

    [Fact]
    public void ToFileSystemPath_TokenizedPath_ResolvesAgainstTheConfiguredRoot()
    {
        var resolved = CreateSut().ToFileSystemPath("${PluginDirectory}/videoconference/Callora.Plugin.VideoConference.dll");

        Assert.Equal(Path.Combine(PluginRoot, "videoconference", "Callora.Plugin.VideoConference.dll"), resolved);
    }

    [Fact]
    public void ToFileSystemPath_AbsolutePath_IsLeftAlone()
    {
        var outside = Path.Combine(Root, "opt", "nuget", "Callora.Plugin.Foreign.dll");

        Assert.Equal(outside, CreateSut().ToFileSystemPath(outside));
    }

    // Der Grund für den Umbau: Derselbe Datenbestand, zwei Umgebungen. Was gespeichert wird,
    // darf nicht davon abhängen, wo der Prozess gerade läuft.
    [Fact]
    public void StoredPath_IsTheSameAcrossTwoEnvironmentsWithDifferentRoots()
    {
        var outsideDocker = new PluginAssemblyPathPortability(new CalloraHostingOptions
        {
            PluginDirectory = Path.Combine(Root, "home", "dev", "Callora-Production", "custom", "plugins"),
            StaticPluginDirectory = Path.Combine(Root, "home", "dev", "Callora-Production", "custom", "static-plugins"),
        });
        var insideDocker = new PluginAssemblyPathPortability(new CalloraHostingOptions
        {
            PluginDirectory = Path.Combine(Root, "app", "custom", "plugins"),
            StaticPluginDirectory = Path.Combine(Root, "app", "custom", "static-plugins"),
        });

        var storedOutside = outsideDocker.ToStoredPath(
            Path.Combine(Root, "home", "dev", "Callora-Production", "custom", "plugins", "videoconference", "x.dll"));

        Assert.Equal("${PluginDirectory}/videoconference/x.dll", storedOutside);
        Assert.Equal(
            Path.Combine(Root, "app", "custom", "plugins", "videoconference", "x.dll"),
            insideDocker.ToFileSystemPath(storedOutside));
    }

    [Fact]
    public void IsUnderPluginRoots_TokenizedPath_IsTrue()
    {
        Assert.True(CreateSut().IsUnderPluginRoots("${StaticPluginDirectory}/Communication/x.dll"));
    }

    // Bestand aus der Zeit vor dem Umbau: absolut gespeichert, aber sehr wohl aus einer Wurzel.
    // Wer das nicht erkennt, lässt ein verschwundenes Plugin für immer installiert stehen.
    [Fact]
    public void IsUnderPluginRoots_AbsolutePathUnderARoot_IsTrue()
    {
        Assert.True(CreateSut().IsUnderPluginRoots(Path.Combine(PluginRoot, "videoconference", "x.dll")));
    }

    [Fact]
    public void IsUnderPluginRoots_AbsolutePathOutsideBothRoots_IsFalse()
    {
        Assert.False(CreateSut().IsUnderPluginRoots(Path.Combine(Root, "opt", "nuget", "x.dll")));
    }
}
