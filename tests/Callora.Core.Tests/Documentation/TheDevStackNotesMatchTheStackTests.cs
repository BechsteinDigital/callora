using Callora.Core.Tests.Cli;
using Xunit;

namespace Callora.Core.Tests.Documentation;

/// <summary>
/// Keeps CLAUDE.md's dev-stack notes tied to the files they describe.
/// </summary>
/// <remarks>
/// <para>
/// The "Fallen, die Zeit gekostet haben" section is the most-read part of that file and the
/// least verifiable: every entry is a claim about behaviour someone once observed. One of
/// them outlived its cause by months — the frontdoor was said to produce a redirect loop on
/// port 5000, which was true while it routed /admin* to a separate Admin shell on 3200. Both
/// shells are gone and the Caddyfile is a single path-neutral reverse_proxy, so both ports
/// serve the same thing. The note stayed, and a reader had no way to tell.
/// </para>
/// <para>
/// These tests bind the remaining claims to the artefacts that make them true, so the next
/// change to the stack breaks the note rather than silently outdating it.
/// </para>
/// </remarks>
public sealed class TheDevStackNotesMatchTheStackTests
{
    private static readonly string RepositoryRoot = ScaffoldedPluginFixture.ResolveRepositoryRoot();

    private static string ReadRepositoryFile(params string[] segments) =>
        File.ReadAllText(Path.Combine([RepositoryRoot, .. segments]));

    [Fact]
    public void TheFrontdoorIsPathNeutralSoNoPortIsSpecial()
    {
        // Directives only. The Caddyfile's comment block deliberately quotes the old
        // per-path routing it replaced — reading the whole file would fail on the very
        // explanation that documents why the routing is gone.
        var directives = string.Join(
            '\n',
            ReadRepositoryFile("ops", "local-frontdoor", "Caddyfile")
                .Split('\n')
                .Where(line => !line.TrimStart().StartsWith('#')));

        // A single catch-all reverse_proxy is what makes "both ports serve the same thing"
        // true. Reintroduce a path matcher and the claim in CLAUDE.md needs rewriting — this
        // test is the reminder.
        Assert.Contains("reverse_proxy", directives, StringComparison.Ordinal);
        Assert.DoesNotContain("/admin", directives, StringComparison.Ordinal);
        Assert.DoesNotContain(":3200", directives, StringComparison.Ordinal);
        Assert.DoesNotContain(":3300", directives, StringComparison.Ordinal);
    }

    [Fact]
    public void TheNoteSaysWhatIsTrueRatherThanBanningTheOldWord()
    {
        var claudeMd = ReadRepositoryFile("CLAUDE.md");

        // Asserting the correct claim is present, not that the old word is absent. Banning
        // "Redirect-Loop" would also ban explaining that it used to happen and why it
        // stopped — which is exactly what this repository asks comments to do. The note
        // keeps the history and states the current behaviour; this pins the latter.
        Assert.Contains("pfadneutral", claudeMd, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("docker-compose.yml", claudeMd, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryPathTheNotesNameExists()
    {
        // The cheap check that would have caught this class of rot: docker-compose.dev.yml
        // was merged away on 2026-08-19 and references to it survived elsewhere. A note
        // pointing at a file that is not there is worse than no note — it reads as current.
        var claudeMd = ReadRepositoryFile("CLAUDE.md");

        var referenced = System.Text.RegularExpressions.Regex
            .Matches(claudeMd, @"`(scripts/[A-Za-z0-9._/-]+\.sh|docker-compose[A-Za-z0-9.-]*\.yml)`")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(referenced);
        foreach (var path in referenced)
        {
            Assert.True(
                File.Exists(Path.Combine(RepositoryRoot, path.Replace('/', Path.DirectorySeparatorChar))),
                $"CLAUDE.md names '{path}', which does not exist.");
        }
    }
}
