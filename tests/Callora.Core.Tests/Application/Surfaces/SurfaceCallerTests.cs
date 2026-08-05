using Callora.Core.Application.Surfaces;

namespace Callora.Core.Tests.Application.Surfaces;

/// <summary>
/// The guest/authenticated split is a type distinction, not a convention
/// (ADR-017 §3): a consumer cannot reach claims by checking that a subject exists,
/// because a guest has one too.
/// </summary>
public sealed class SurfaceCallerTests
{
    [Fact]
    public void AGuestCarriesASubjectButNoIdentity()
    {
        SurfaceCaller caller = new GuestSurfaceCaller(
            new SurfaceSubject(SurfaceIdentityIssuers.Guest, "g-1"));

        Assert.NotNull(caller.Subject);
        Assert.IsNotType<AuthenticatedSurfaceCaller>(caller);
    }

    [Fact]
    public void SubjectKeyCombinesIssuerAndSubject()
    {
        var subject = new SurfaceSubject("crm.example", "lead-42");

        Assert.Equal("crm.example|lead-42", subject.Key);
    }

    [Fact]
    public void SameSubjectIdFromDifferentIssuers_AreDifferentSubjects()
    {
        var fromCrm = new SurfaceSubject("crm.example", "42");
        var fromPortal = new SurfaceSubject("portal.example", "42");

        Assert.NotEqual(fromCrm, fromPortal);
        Assert.NotEqual(fromCrm.Key, fromPortal.Key);
    }

    [Fact]
    public void OnlyTheHostMayUseTheReservedIssuerNamespace()
    {
        Assert.True(SurfaceIdentityIssuers.IsReserved(SurfaceIdentityIssuers.Guest));
        Assert.True(SurfaceIdentityIssuers.IsReserved(SurfaceIdentityIssuers.Host));
        Assert.False(SurfaceIdentityIssuers.IsReserved("crm.example"));
    }
}
