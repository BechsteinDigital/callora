using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// Runs the surface-rendering tests one at a time.
/// <para>
/// A render costs ~110 ms of CPU: the Nunjucks engine compiles the base chain — six to nine
/// templates — inside a JS interpreter, on a fresh engine per render. The renderer's own limit is
/// WALL-CLOCK, so sixteen of these racing each other on one machine turns 110 ms into seconds and
/// the limit fires on work that was never slow, only starved.
/// </para>
/// <para>
/// Serialising them removes the starvation without touching the production limit, which is the
/// honest trade: these tests assert what a render PRODUCES, and none of them is about concurrency.
/// Weakening the limit instead would weaken the only thing standing between a hostile template and
/// a pinned core.
/// </para>
/// <para>
/// The underlying cost is real and stays: compiling the templates at build time rather than per
/// render is the fix, and it is its own piece of work.
/// </para>
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SurfaceRenderingCollection
{
    public const string Name = "surface-rendering";
}
