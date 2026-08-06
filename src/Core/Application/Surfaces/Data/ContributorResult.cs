namespace Callora.Core.Application.Surfaces.Data;

/// <summary>One contributor's outcome: what it said, or that it could not say anything.</summary>
internal readonly record struct ContributorResult(
    IHostSurfaceDataContributor Contributor,
    SurfaceDataResult? Result,
    bool Failed);
