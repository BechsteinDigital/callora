namespace Callora.Core.Domain.Workspaces;

/// <summary>
/// The one place that answers "does this node demand a sign-in?" (ADR-023).
/// <para>
/// Four call sites used to compare against <c>Authenticated</c> themselves. That worked while
/// exactly one value demanded a sign-in; with two, every one of them would have had to grow the
/// same second comparison — and the one that got forgotten would serve a protected surface
/// anonymously. A 200 with the wrong content is the failure mode nobody sees.
/// </para>
/// </summary>
public static class SurfaceAuthenticationExtensions
{
    /// <summary>
    /// Whether a visitor must be signed in. True for everything except <see cref="SurfaceAuthentication.Public"/>
    /// — written as an exclusion on purpose, so a value added later demands a sign-in until
    /// someone decides otherwise.
    /// </summary>
    /// <param name="authentication">The node's effective authentication.</param>
    public static bool RequiresSignIn(this SurfaceAuthentication authentication) =>
        authentication != SurfaceAuthentication.Public;
}
