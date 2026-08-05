using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Surfaces;
using Microsoft.Extensions.Time.Testing;

namespace Callora.Core.Tests.Application.Surfaces;

/// <summary>
/// What a plugin returns is a candidate, not an identity: the host decides the
/// issuer namespace it is allowed to use, how long it may live, and whether the
/// shape holds (ADR-017 §4). These tests pin the boundary, because everything
/// downstream — session, render context, surface API — trusts what passes it.
/// </summary>
public sealed class SurfaceIdentityNormalizerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Anonymous_IsNotARejection()
    {
        var result = Normalize(HostSurfaceIdentityResult.Anonymous);

        Assert.False(result.IsAccepted);
        Assert.Equal(SurfaceIdentityRejectionReason.NotIdentified, result.Reason);
    }

    [Fact]
    public void ValidCandidate_BecomesAnAuthenticatedCaller()
    {
        var result = Normalize(Candidate());

        var caller = Assert.IsType<AuthenticatedSurfaceCaller>(result.Caller);
        Assert.Equal("crm.example", caller.Subject.Issuer);
        Assert.Equal("lead-42", caller.Subject.SubjectId);
        Assert.Equal("Erika Muster", caller.Identity.DisplayName);
        Assert.Equal("password", caller.Identity.AuthenticationMethod);
    }

    [Fact]
    public void PluginProvider_CannotIssueUnderTheHostNamespace()
    {
        var result = Normalize(Candidate(issuer: "callora.host"));

        Assert.Equal(SurfaceIdentityRejectionReason.ReservedIssuer, result.Reason);
    }

    [Fact]
    public void HostSource_MayIssueUnderTheHostNamespace()
    {
        var result = Normalize(Candidate(issuer: "callora.host"), allowReservedIssuer: true);

        Assert.True(result.IsAccepted);
        Assert.Equal("callora.host", result.Caller!.Subject.Issuer);
    }

    [Theory]
    [InlineData("")]
    [InlineData("CRM.Example")]
    [InlineData("crm.example.")]
    [InlineData("crm example")]
    [InlineData("crm|example")]
    public void MalformedIssuer_IsRejected(string issuer)
    {
        var result = Normalize(Candidate(issuer: issuer));

        Assert.Equal(SurfaceIdentityRejectionReason.InvalidIssuer, result.Reason);
    }

    [Fact]
    public void MissingSubject_IsRejected()
    {
        var result = Normalize(Candidate(subjectId: "   "));

        Assert.Equal(SurfaceIdentityRejectionReason.InvalidSubject, result.Reason);
    }

    [Fact]
    public void SubjectWithControlCharacters_IsRejected()
    {
        var result = Normalize(Candidate(subjectId: "lead\n42"));

        Assert.Equal(SurfaceIdentityRejectionReason.InvalidSubject, result.Reason);
    }

    [Fact]
    public void MissingAuthenticationMethod_IsRejected()
    {
        var result = Normalize(Candidate(authenticationMethod: ""));

        Assert.Equal(SurfaceIdentityRejectionReason.InvalidAuthenticationMethod, result.Reason);
    }

    [Fact]
    public void AlreadyExpiredCandidate_IsRejected()
    {
        var result = Normalize(Candidate(expiresAt: Now.AddSeconds(-1)));

        Assert.Equal(SurfaceIdentityRejectionReason.Expired, result.Reason);
    }

    [Fact]
    public void AuthenticationTimeBeyondSkew_IsRejected()
    {
        var result = Normalize(Candidate(authenticatedAt: Now.AddMinutes(5)));

        Assert.Equal(SurfaceIdentityRejectionReason.InvalidTimestamps, result.Reason);
    }

    [Fact]
    public void AuthenticationTimeWithinSkew_IsAcceptedAndPulledBackToNow()
    {
        var result = Normalize(Candidate(authenticatedAt: Now.AddSeconds(30)));

        Assert.True(result.IsAccepted);
        Assert.Equal(Now, result.Caller!.Identity.AuthenticatedAtUtc);
    }

    [Fact]
    public void ExpiryBeyondTheHostMaximum_IsClamped()
    {
        var options = new SurfaceIdentityOptions { MaxIdentityLifetime = TimeSpan.FromHours(1) };

        var result = Normalize(Candidate(expiresAt: Now.AddDays(30)), options: options);

        Assert.Equal(Now.AddHours(1), result.Caller!.Identity.ExpiresAtUtc);
    }

    [Fact]
    public void ShorterProviderExpiry_Wins()
    {
        var result = Normalize(Candidate(expiresAt: Now.AddMinutes(5)));

        Assert.Equal(Now.AddMinutes(5), result.Caller!.Identity.ExpiresAtUtc);
    }

    [Fact]
    public void MissingDisplayName_FallsBackToTheSubject()
    {
        var result = Normalize(Candidate(displayName: "  "));

        Assert.Equal("lead-42", result.Caller!.Identity.DisplayName);
    }

    private static SurfaceIdentityNormalization Normalize(
        HostSurfaceIdentityResult candidate,
        bool allowReservedIssuer = false,
        SurfaceIdentityOptions? options = null)
    {
        var effective = options ?? new SurfaceIdentityOptions();
        var normalizer = new SurfaceIdentityNormalizer(
            effective,
            new SurfaceIdentityClaimNormalizer(effective),
            new FakeTimeProvider(Now));

        return normalizer.Normalize(candidate, allowReservedIssuer);
    }

    private static HostSurfaceIdentityResult Candidate(
        string issuer = "crm.example",
        string subjectId = "lead-42",
        string authenticationMethod = "password",
        string? displayName = "Erika Muster",
        DateTimeOffset? authenticatedAt = null,
        DateTimeOffset? expiresAt = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? claims = null) =>
        HostSurfaceIdentityResult.Identified(
            issuer,
            subjectId,
            authenticationMethod,
            authenticatedAt ?? Now.AddMinutes(-1),
            expiresAt ?? Now.AddHours(2),
            displayName,
            claims);
}
