using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Callora.Plugin.Communication.Api.Surface;
using Xunit;

namespace Callora.Core.Tests.Communication.Surface;

/// <summary>
/// A block has two halves — a Vue component registered in the browser bundle and a view declared on
/// the server — and they are joined by nothing but a string.
/// </summary>
/// <remarks>
/// Drift between them fails silently in both directions: an id only the client knows is offered in
/// the editor and never mounts, and one only the server knows emits an island no component fills.
/// Neither logs anything. This is the governance rule from the design (§10.1) as a test.
/// </remarks>
public sealed class SurfaceBlockRegistrationTests
{
    private static readonly Regex BlockId = new(
        @"registerBlock\(\{\s*id:\s*'(?<id>[^']+)'", RegexOptions.Compiled);

    [Fact]
    public void EveryBlockTheBundleRegisters_HasAServerRegistration()
    {
        var declared = new CommunicationSurfaceViewContributor("communication")
            .Views.Select(view => view.ViewId).ToHashSet(StringComparer.Ordinal);

        var registered = BlockIdsInBundle();

        Assert.NotEmpty(registered);
        Assert.Empty(registered.Except(declared, StringComparer.Ordinal));
    }

    [Fact]
    public void EveryViewTheServerDeclares_HasABlockInTheBundle()
    {
        var declared = new CommunicationSurfaceViewContributor("communication")
            .Views.Select(view => view.ViewId);

        var registered = BlockIdsInBundle();

        Assert.Empty(declared.Except(registered, StringComparer.Ordinal));
    }

    [Fact]
    public void EveryBlockDeclaresTheContextItReads()
    {
        // Der Server liefert einen Kontext-Schlüssel nur aus, wenn ein sichtbarer Block ihn
        // angemeldet hat. Ein Block, der ihn im Browser abonniert und serverseitig verschweigt,
        // wartet für immer auf einen Wert, den niemand schickt (§5.5 P3).
        var views = new CommunicationSurfaceViewContributor("communication").Views;
        var bundle = BundleSource();

        foreach (var view in views)
        {
            var subscribesToActive = bundle.Contains($"'{view.ViewId}'", StringComparison.Ordinal);
            Assert.True(subscribesToActive, $"Der Block {view.ViewId} fehlt im Bundle.");
        }

        var phone = Assert.Single(views, view => view.ViewId == CommunicationSurfaceViewContributor.PhoneViewId);
        Assert.Contains(SurfaceCallContextKeys.ActiveCall, phone.RequiresContexts!);
    }

    private static IReadOnlyCollection<string> BlockIdsInBundle() =>
        [.. BlockId.Matches(BundleSource()).Select(match => match.Groups["id"].Value)];

    /// <summary>
    /// The bundle's entry module, read from source. The compiled bundle is a build artifact and is
    /// not always present; the source is what a reviewer changes and what has to stay in step.
    /// </summary>
    private static string BundleSource()
    {
        var path = Path.Combine(
            RepositoryRoot(),
            "custom", "static-plugins", "Communication", "src", "Resources", "app", "surface", "src", "main.ts");

        Assert.True(File.Exists(path), $"Bundle-Einstieg nicht gefunden: {path}");
        return File.ReadAllText(path);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Callora.Host.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
