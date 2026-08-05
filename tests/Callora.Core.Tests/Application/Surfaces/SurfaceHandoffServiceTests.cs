using Callora.Core.Application.Surfaces;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.Time.Testing;

namespace Callora.Core.Tests.Application.Surfaces;

/// <summary>
/// The handover across hosts (ADR-017 §8.4). What travels is a secret that is good
/// once, for seconds, at one host. These tests pin each of those three, because
/// dropping any one of them turns the ticket into the roaming bearer token the design
/// exists to avoid.
/// </summary>
public sealed class SurfaceHandoffServiceTests
{
    private const string WorkspaceKey = "workspace-a";
    private const string TenantKey = "tenant-a";
    private const string SourceSurface = "crm";
    private const string TargetSurface = "meet";
    private const string TargetHost = "meet.example.de";
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AnAuthenticatedCallerGetsATicketBoundToTheTargetHost()
    {
        var fixture = new HandoffFixture();

        var issue = await fixture.Service.IssueAsync(fixture.Source(), TargetSurface);

        Assert.Equal(SurfaceHandoffStatus.Ok, issue.Status);
        Assert.Equal(TargetHost, issue.TargetAudience);
        Assert.Equal(Now.AddSeconds(60), issue.ExpiresAtUtc);
        Assert.NotNull(issue.Secret);
    }

    [Fact]
    public async Task TheStoredTicketNeverHoldsTheSecret()
    {
        var fixture = new HandoffFixture();

        var issue = await fixture.Service.IssueAsync(fixture.Source(), TargetSurface);

        Assert.DoesNotContain(
            fixture.Tickets.Tickets,
            ticket => ticket.ToString().Contains(issue.Secret!, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AGuestCannotBeHandedOver()
    {
        var fixture = new HandoffFixture();

        var issue = await fixture.Service.IssueAsync(fixture.Source(guest: true), TargetSurface);

        Assert.Equal(SurfaceHandoffStatus.NotAuthenticated, issue.Status);
        Assert.Empty(fixture.Tickets.Tickets);
    }

    [Fact]
    public async Task AnUnknownTargetIsRefused()
    {
        var fixture = new HandoffFixture();

        var issue = await fixture.Service.IssueAsync(fixture.Source(), "no-such-surface");

        Assert.Equal(SurfaceHandoffStatus.TargetUnavailable, issue.Status);
    }

    [Fact]
    public async Task ATargetWithoutAPublicHostIsRefused()
    {
        var fixture = new HandoffFixture();
        fixture.SeedTarget(publicHost: null, publicBaseUrl: null);

        var issue = await fixture.Service.IssueAsync(fixture.Source(), TargetSurface);

        Assert.Equal(SurfaceHandoffStatus.TargetUnavailable, issue.Status);
    }

    [Fact]
    public async Task ATicketNeverOutlivesTheIdentityItCarries()
    {
        var fixture = new HandoffFixture();

        var issue = await fixture.Service.IssueAsync(
            fixture.Source(identityExpiresAtUtc: Now.AddSeconds(10)), TargetSurface);

        Assert.Equal(Now.AddSeconds(10), issue.ExpiresAtUtc);
    }

    [Fact]
    public async Task RedeemingYieldsTheCarriedIdentityAtTheTarget()
    {
        var fixture = new HandoffFixture();
        var issue = await fixture.Service.IssueAsync(fixture.Source(), TargetSurface);

        var redemption = await fixture.Service.RedeemAsync(issue.Secret, TargetHost);

        Assert.Equal(SurfaceHandoffStatus.Ok, redemption.Status);
        Assert.Equal(TargetSurface, redemption.Surface!.SurfaceKey);
        Assert.Equal(TenantKey, redemption.Surface.TenantKey);
        Assert.Equal("lead-42", redemption.Caller!.Subject.SubjectId);
        Assert.Equal(["agent"], redemption.Caller.Identity.Claims["crm.roles"]);
    }

    [Fact]
    public async Task ATicketWorksExactlyOnce()
    {
        var fixture = new HandoffFixture();
        var issue = await fixture.Service.IssueAsync(fixture.Source(), TargetSurface);

        Assert.Equal(SurfaceHandoffStatus.Ok, (await fixture.Service.RedeemAsync(issue.Secret, TargetHost)).Status);
        Assert.Equal(
            SurfaceHandoffStatus.TicketInvalid,
            (await fixture.Service.RedeemAsync(issue.Secret, TargetHost)).Status);
    }

    [Fact]
    public async Task ATicketPresentedOnTheWrongHostIsRefusedAndBurned()
    {
        var fixture = new HandoffFixture();
        var issue = await fixture.Service.IssueAsync(fixture.Source(), TargetSurface);

        var wrongHost = await fixture.Service.RedeemAsync(issue.Secret, "evil.example.com");

        Assert.Equal(SurfaceHandoffStatus.AudienceMismatch, wrongHost.Status);
        // Consumed regardless: a refused presentation must not be retryable elsewhere.
        Assert.Equal(
            SurfaceHandoffStatus.TicketInvalid,
            (await fixture.Service.RedeemAsync(issue.Secret, TargetHost)).Status);
    }

    [Fact]
    public async Task AnExpiredTicketIsRefused()
    {
        var fixture = new HandoffFixture();
        var issue = await fixture.Service.IssueAsync(fixture.Source(), TargetSurface);

        fixture.Clock.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(
            SurfaceHandoffStatus.TicketInvalid,
            (await fixture.Service.RedeemAsync(issue.Secret, TargetHost)).Status);
    }

    [Fact]
    public async Task AnUnknownSecretIsRefused()
    {
        var fixture = new HandoffFixture();

        Assert.Equal(
            SurfaceHandoffStatus.TicketInvalid,
            (await fixture.Service.RedeemAsync(SurfaceHandoffSecret.Create(), TargetHost)).Status);
    }

    [Fact]
    public async Task ATicketPredatingTheTargetsProviderIsRefused()
    {
        var fixture = new HandoffFixture();
        var issue = await fixture.Service.IssueAsync(fixture.Source(), TargetSurface);

        fixture.SeedTarget(identityAssignedAtUtc: Now.AddSeconds(1));

        Assert.Equal(
            SurfaceHandoffStatus.TicketInvalid,
            (await fixture.Service.RedeemAsync(issue.Secret, TargetHost)).Status);
    }

    [Fact]
    public async Task ATicketForADeactivatedTargetIsRefused()
    {
        var fixture = new HandoffFixture();
        var issue = await fixture.Service.IssueAsync(fixture.Source(), TargetSurface);

        fixture.SeedTarget(isActive: false);

        Assert.Equal(
            SurfaceHandoffStatus.TargetUnavailable,
            (await fixture.Service.RedeemAsync(issue.Secret, TargetHost)).Status);
    }

    private sealed class HandoffFixture
    {
        private readonly InMemoryWorkspaceSurfaceStore _surfaces = new();

        public HandoffFixture()
        {
            Clock = new FakeTimeProvider(Now);
            Tickets = new InMemorySurfaceHandoffTicketStore();
            SeedTarget();
            Service = new SurfaceHandoffService(_surfaces, Tickets, new SurfaceIdentityOptions(), Clock);
        }

        public FakeTimeProvider Clock { get; }

        public InMemorySurfaceHandoffTicketStore Tickets { get; }

        public SurfaceHandoffService Service { get; }

        public void SeedTarget(
            string? publicHost = TargetHost,
            string? publicBaseUrl = null,
            bool isActive = true,
            DateTimeOffset? identityAssignedAtUtc = null) =>
            _surfaces.Seed(new WorkspaceSurfaceSnapshot(
                Guid.NewGuid(), WorkspaceKey, TargetSurface, "Meet", "spa",
                publicBaseUrl, publicHost, "/", SurfaceAccessMode.Authenticated, "de",
                null, null, null, null, isActive, Now, Now)
            {
                TenantKey = TenantKey,
                IdentityAssignedAtUtc = identityAssignedAtUtc ?? Now.AddDays(-1),
            });

        public SurfaceCallerContext Source(bool guest = false, DateTimeOffset? identityExpiresAtUtc = null)
        {
            SurfaceCaller caller = guest
                ? new GuestSurfaceCaller(new SurfaceSubject(SurfaceIdentityIssuers.Guest, "g-1"))
                : new AuthenticatedSurfaceCaller(
                    new SurfaceSubject("crm.example", "lead-42"),
                    new SurfaceIdentity(
                        "Erika Muster",
                        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
                        {
                            ["crm.roles"] = ["agent"],
                        },
                        "password",
                        Now.AddMinutes(-1),
                        identityExpiresAtUtc ?? Now.AddHours(2)));

            return new SurfaceCallerContext(caller, TenantKey, WorkspaceKey, SourceSurface);
        }
    }
}
