using Callora.Core.Application.Plugins;
using System.Runtime.CompilerServices;
using Xunit;

namespace Callora.Core.Tests.Application.Plugins;

/// <summary>
/// Verifies the collection check that turns a silently pinned assembly load
/// context into a visible failure (P0-4).
/// </summary>
public sealed class AssemblyLoadContextUnloadTests
{
    [Fact]
    public void WaitForCollection_UnrootedTarget_ReportsCollected()
    {
        var reference = CreateUnrootedReference();

        Assert.True(AssemblyLoadContextUnload.WaitForCollection(reference));
    }

    [Fact]
    public void WaitForCollection_PinnedTarget_ReportsNotCollected()
    {
        var pinned = new object();
        var reference = new WeakReference(pinned);

        Assert.False(AssemblyLoadContextUnload.WaitForCollection(reference, maxAttempts: 3));

        GC.KeepAlive(pinned);
    }

    // Non-inlined so the created object has no strong root once this returns.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateUnrootedReference() => new(new object());
}
