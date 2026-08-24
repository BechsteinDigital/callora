using Callora.Core.Extensibility;
using Xunit;

namespace Callora.Core.Tests.Documentation;

/// <summary>
/// The surface file has to show a deprecation, or the middle rung is invisible where it
/// matters most — in the reviewable diff a human is asked to judge.
/// </summary>
/// <remarks>
/// Exercised against fixtures in this assembly rather than against a real deprecation in
/// the platform. There is currently nothing on the extension surface that deserves
/// deprecating, and inventing one would announce a removal to plugin authors that nobody
/// intends — a promise made to test a mechanism.
/// </remarks>
public sealed class TheSurfaceCarriesTheAnnouncementTests
{
    [Fact]
    public void ADeprecatedMemberCarriesItsAnnouncement()
    {
        var surface = TheExtensionSurfaceMatchesItsContractVersionTests
            .DescribeExtensionSurface(typeof(TheSurfaceCarriesTheAnnouncementTests).Assembly);

        var line = Assert.Single(
            surface.Split('\n'),
            l => l.Contains(nameof(IDeprecatedFixture.OldWay), StringComparison.Ordinal));
        Assert.Contains("# deprecated since 0.9.2, error in v3", line, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUndeprecatedMemberOnTheSameTypeCarriesNothing()
    {
        var surface = TheExtensionSurfaceMatchesItsContractVersionTests
            .DescribeExtensionSurface(typeof(TheSurfaceCarriesTheAnnouncementTests).Assembly);

        var line = Assert.Single(
            surface.Split('\n'),
            l => l.Contains(nameof(IDeprecatedFixture.NewWay), StringComparison.Ordinal));
        Assert.DoesNotContain("# deprecated", line, StringComparison.Ordinal);
    }

    [Fact]
    public void DeprecatingTheTypeCoversEveryMemberOfIt()
    {
        // Otherwise retiring a type means marking each member by hand, and the one that is
        // forgotten is the one someone keeps using.
        var surface = TheExtensionSurfaceMatchesItsContractVersionTests
            .DescribeExtensionSurface(typeof(TheSurfaceCarriesTheAnnouncementTests).Assembly);

        var lines = surface.Split('\n')
            .Where(l => l.Contains(nameof(IWholeTypeDeprecatedFixture), StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(lines);
        Assert.All(lines, l => Assert.Contains("# deprecated since 0.9.2, error in v3", l, StringComparison.Ordinal));
    }
}

[CalloraExtensible("Fixture — exercises the deprecation announcement in the surface file")]
public interface IDeprecatedFixture
{
    /// <summary>Superseded, still working.</summary>
    [CalloraDeprecated("0.9.2", "v3", Replacement = "IDeprecatedFixture.NewWay")]
    bool OldWay();

    /// <summary>What to use instead.</summary>
    bool NewWay();
}

[CalloraExtensible("Fixture — exercises a whole-type deprecation")]
[CalloraDeprecated("0.9.2", "v3", Replacement = "IDeprecatedFixture")]
public interface IWholeTypeDeprecatedFixture
{
    /// <summary>Inherits the type's announcement.</summary>
    bool Anything();
}
