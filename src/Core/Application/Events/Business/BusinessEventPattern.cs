using System.Text;
using System.Text.RegularExpressions;

namespace Callora.Core.Application.Events.Business;

/// <summary>
/// Matches a subscription's event pattern against the events a host actually publishes.
/// </summary>
/// <remarks>
/// <para>
/// Exists so a flow or webhook naming an event nobody publishes can be told apart from one
/// that is simply ahead of its plugin. Both endpoints validate the SHAPE of the string;
/// neither could say whether such an event exists, so a misspelling and a deliberate
/// early subscription looked identical.
/// </para>
/// <para>
/// Derived, never stored. Whether a pattern matches is a property of the moment it is asked,
/// and it becomes true on its own once the plugin arrives — no field to update, nothing to
/// clean up when a plugin is removed.
/// </para>
/// </remarks>
public static class BusinessEventPattern
{
    /// <summary>Whether <paramref name="pattern"/> covers at least one known event name.</summary>
    public static bool MatchesAny(string pattern, IReadOnlyCollection<string> knownEventNames)
    {
        ArgumentNullException.ThrowIfNull(knownEventNames);

        if (string.IsNullOrWhiteSpace(pattern) || knownEventNames.Count == 0)
        {
            return false;
        }

        var matcher = Compile(pattern.Trim());
        return knownEventNames.Any(name => !string.IsNullOrWhiteSpace(name) && matcher.IsMatch(name));
    }

    // Only '*' is a wildcard; everything else is literal. Handing the pattern to Regex
    // unescaped would make "workspace.created" match "workspaceXcreated" through the dot,
    // and would let a hostile pattern cost real time.
    private static Regex Compile(string pattern)
    {
        var expression = new StringBuilder("^");
        foreach (var character in pattern)
        {
            expression.Append(character == '*' ? ".*" : Regex.Escape(character.ToString()));
        }

        return new Regex(
            expression.Append('$').ToString(),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
    }
}
