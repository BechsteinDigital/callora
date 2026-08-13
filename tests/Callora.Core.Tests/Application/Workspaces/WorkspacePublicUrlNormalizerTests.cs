using Callora.Core.Application.Workspaces;
using Xunit;

namespace Callora.Core.Tests.Application.Workspaces;

/// <summary>
/// Die Eingabe des Betreibers für die öffentliche Adresse eines Workspaces. Sie wird genau
/// einmal geprüft — danach steht das Ergebnis in der Default-Fläche und der Renderpfad vergleicht
/// es ungeprüft gegen jede Anfrage.
/// </summary>
public sealed class WorkspacePublicUrlNormalizerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyInput_YieldsWildcardRoute(string? input)
    {
        Assert.True(WorkspacePublicUrlNormalizer.TryNormalize(input, out var descriptor, out var error));

        Assert.Null(error);
        Assert.Null(descriptor.PublicHost);
        Assert.Equal("/", descriptor.PublicPathPrefix);
    }

    [Fact]
    public void HostWithPath_SplitsIntoHostAndPrefix()
    {
        Assert.True(WorkspacePublicUrlNormalizer.TryNormalize("Dialer.Example.DE/portal/", out var descriptor, out var error));

        Assert.Null(error);
        Assert.Equal("dialer.example.de", descriptor.PublicHost);
        Assert.Equal("/portal", descriptor.PublicPathPrefix);
    }

    [Fact]
    public void PathOnlyInput_KeepsHostUnset()
    {
        Assert.True(WorkspacePublicUrlNormalizer.TryNormalize("/shop/", out var descriptor, out var error));

        Assert.Null(error);
        Assert.Null(descriptor.PublicHost);
        Assert.Equal("/shop", descriptor.PublicPathPrefix);
    }

    /// <summary>
    /// Der Kern von #249: Der Pfad-Zweig kehrt vor der Uri-Prüfung zurück, also muss er selbst
    /// ablehnen. Sonst wird der Query-Teil Bestandteil des Routenpräfixes und die Fläche ist
    /// unerreichbar — Request.Path führt keine Query mit, der Vergleich kann nie zutreffen.
    /// </summary>
    [Theory]
    [InlineData("/shop?ref=1")]
    [InlineData("/shop#top")]
    public void PathOnlyInput_WithQueryOrFragment_IsRejected(string input)
    {
        Assert.False(WorkspacePublicUrlNormalizer.TryNormalize(input, out var descriptor, out var error));

        Assert.Equal("PublicBaseUrl must not contain query string or fragment.", error);
        Assert.Null(descriptor.PublicHost);
        Assert.Equal("/", descriptor.PublicPathPrefix);
    }

    /// <summary>
    /// Dieselbe Eingabe ohne führenden Schrägstrich wurde schon immer abgelehnt. Die beiden
    /// Zweige dürfen sich nicht unterscheiden, sonst wirkt die Regel willkürlich.
    /// </summary>
    [Fact]
    public void HostInput_WithQuery_IsRejectedTheSameWay()
    {
        Assert.False(WorkspacePublicUrlNormalizer.TryNormalize("shop.example.de?ref=1", out _, out var error));

        Assert.Equal("PublicBaseUrl must not contain query string or fragment.", error);
    }
}
