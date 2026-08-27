using Callora.Core.Application.Events.Business;
using Xunit;

namespace Callora.Core.Tests.Application.Events;

/// <summary>
/// A flow or webhook naming an event nobody publishes is accepted today and never fires.
/// Both endpoints check the SHAPE of the string; neither asks whether such an event exists.
/// </summary>
/// <remarks>
/// <para>
/// Refusing would be wrong. Subscribing to <c>communication.call.ringing</c> before the
/// Communication plugin is installed is legitimate — arguably the normal order when preparing
/// a workspace. What is not legitimate is <c>workspace.creted</c>, and today the two are
/// indistinguishable.
/// </para>
/// <para>
/// So the answer is derived, never stored: whether a pattern matches anything known is a
/// property of the moment it is asked, and it becomes true on its own when the plugin
/// arrives. The same reasoning <see cref="Callora.Core.Application.Plugins.PluginAvailability"/>
/// applies to entitlement — participate in the derivation, not in the write.
/// </para>
/// </remarks>
public sealed class AnUnmatchedSubscriptionIsVisibleTests
{
    private static readonly string[] Known =
    [
        "workspace.created",
        "workspace.suspended",
        "user.invited"
    ];

    [Theory]
    [InlineData("workspace.created")]
    [InlineData("user.invited")]
    public void An_exact_name_matches(string pattern)
    {
        Assert.True(BusinessEventPattern.MatchesAny(pattern, Known));
    }

    [Fact]
    public void A_misspelling_does_not()
    {
        Assert.False(BusinessEventPattern.MatchesAny("workspace.creted", Known));
    }

    [Theory]
    [InlineData("workspace.*")]
    [InlineData("*.created")]
    [InlineData("*")]
    [InlineData("workspace.*ed")]
    public void A_wildcard_matches_what_it_covers(string pattern)
    {
        // The regex on the endpoint allows '*', so patterns must be matched against the
        // catalogue rather than compared for equality.
        Assert.True(BusinessEventPattern.MatchesAny(pattern, Known));
    }

    [Fact]
    public void A_wildcard_covering_nothing_does_not_match()
    {
        Assert.False(BusinessEventPattern.MatchesAny("billing.*", Known));
    }

    [Fact]
    public void Matching_ignores_case_like_the_rest_of_the_event_path()
    {
        Assert.True(BusinessEventPattern.MatchesAny("Workspace.Created", Known));
    }

    [Fact]
    public void Nothing_matches_an_empty_catalogue()
    {
        // A host with no events yet: every subscription reads as unmatched, which is
        // accurate rather than alarming — nothing can fire.
        Assert.False(BusinessEventPattern.MatchesAny("workspace.created", []));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_pattern_matches_nothing(string pattern)
    {
        Assert.False(BusinessEventPattern.MatchesAny(pattern, Known));
    }

    [Fact]
    public void A_pattern_with_regex_characters_is_taken_literally()
    {
        // Only '*' is a wildcard. Treating the pattern as a regex would make "workspace.created"
        // match "workspaceXcreated" through the dot, and a hostile pattern could be expensive.
        Assert.False(BusinessEventPattern.MatchesAny("workspace+created", Known));
        Assert.False(BusinessEventPattern.MatchesAny("workspaceXcreated", Known));
    }
}
