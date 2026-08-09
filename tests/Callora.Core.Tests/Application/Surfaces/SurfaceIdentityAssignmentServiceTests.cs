using Callora.Core.Application.Surfaces;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Plugins;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Tests.Support;

namespace Callora.Core.Tests.Application.Surfaces;

/// <summary>
/// Assigning an identity provider is not a plain setter (ADR-017 §5, §6.3): only a
/// plugin declaring <c>surface.identity</c> can take the job, and every change ends
/// the sessions the previous provider vouched for.
/// </summary>
public sealed class SurfaceIdentityAssignmentServiceTests
{
    private const string WorkspaceKey = "workspace-a";
    private const string SurfaceKey = "portal";

    [Fact]
    public async Task CandidatesAreFilteredToPluginsDeclaringTheCapability()
    {
        var fixture = await CreateAsync();
        await fixture.InstallAsync("crm", SurfaceIdentityCapability.Key);
        await fixture.InstallAsync("communication", "communication.foundation");

        var candidates = await fixture.Service.ListCandidatesAsync(WorkspaceKey);

        var candidate = Assert.Single(candidates);
        Assert.Equal("crm", candidate.PluginId);
        Assert.True(candidate.IsAvailable);
    }

    [Fact]
    public async Task AnUnavailableCandidateIsListedButMarked()
    {
        var fixture = await CreateAsync(unavailablePluginIds: ["crm"]);
        await fixture.InstallAsync("crm", SurfaceIdentityCapability.Key);

        var candidate = Assert.Single(await fixture.Service.ListCandidatesAsync(WorkspaceKey));

        Assert.False(candidate.IsAvailable);
    }

    [Fact]
    public async Task AssigningStoresTheProvenanceQuartet()
    {
        var fixture = await CreateAsync();
        await fixture.InstallAsync("crm", SurfaceIdentityCapability.Key);

        var result = await fixture.Service.AssignAsync(
            WorkspaceKey, SurfaceKey, "crm", "operator@example.de");

        Assert.Equal(SurfaceIdentityAssignmentStatus.Ok, result.Status);
        Assert.Equal("crm", result.Assignment!.PluginId);
        Assert.Equal("operator@example.de", result.Assignment.AssignedBy);
        Assert.NotNull(result.Assignment.AssignedAtUtc);
    }

    [Fact]
    public async Task APluginWithoutTheCapabilityIsRefused()
    {
        var fixture = await CreateAsync();
        await fixture.InstallAsync("communication", "communication.foundation");

        var result = await fixture.Service.AssignAsync(
            WorkspaceKey, SurfaceKey, "communication", "operator@example.de");

        Assert.Equal(SurfaceIdentityAssignmentStatus.CapabilityMissing, result.Status);
    }

    [Fact]
    public async Task AnUninstalledPluginIsRefused()
    {
        var fixture = await CreateAsync();

        var result = await fixture.Service.AssignAsync(
            WorkspaceKey, SurfaceKey, "ghost", "operator@example.de");

        Assert.Equal(SurfaceIdentityAssignmentStatus.PluginNotFound, result.Status);
    }

    [Fact]
    public async Task AnUnknownSurfaceIsDistinguishedFromAnUnknownWorkspace()
    {
        var fixture = await CreateAsync();

        Assert.Equal(
            SurfaceIdentityAssignmentStatus.SurfaceNotFound,
            (await fixture.Service.GetAsync(WorkspaceKey, "no-such-surface")).Status);
        Assert.Equal(
            SurfaceIdentityAssignmentStatus.WorkspaceNotFound,
            (await fixture.Service.GetAsync("no-such-workspace", SurfaceKey)).Status);
    }

    [Fact]
    public async Task ChangingTheProviderEndsTheSessionsItVouchedFor()
    {
        var fixture = await CreateAsync();
        await fixture.InstallAsync("crm", SurfaceIdentityCapability.Key);
        await fixture.InstallAsync("portal", SurfaceIdentityCapability.Key);
        await fixture.Service.AssignAsync(WorkspaceKey, SurfaceKey, "crm", "op");
        await fixture.SeedSessionAsync();

        var result = await fixture.Service.AssignAsync(WorkspaceKey, SurfaceKey, "portal", "op");

        Assert.Equal(1, result.RevokedSessions);
        Assert.Empty(fixture.Sessions.Sessions);
    }

    [Fact]
    public async Task ClearingAlsoEndsTheSessions()
    {
        var fixture = await CreateAsync();
        await fixture.InstallAsync("crm", SurfaceIdentityCapability.Key);
        await fixture.Service.AssignAsync(WorkspaceKey, SurfaceKey, "crm", "op");
        await fixture.SeedSessionAsync();

        var result = await fixture.Service.ClearAsync(WorkspaceKey, SurfaceKey, "op");

        Assert.Equal(SurfaceIdentityAssignmentStatus.Ok, result.Status);
        Assert.Null(result.Assignment!.PluginId);
        Assert.Equal(1, result.RevokedSessions);
    }

    [Fact]
    public async Task AnAssignedButUnavailablePluginIsReportedAsClosed()
    {
        var fixture = await CreateAsync();
        await fixture.InstallAsync("crm", SurfaceIdentityCapability.Key);
        await fixture.Service.AssignAsync(WorkspaceKey, SurfaceKey, "crm", "op");

        // The plugin lapses (entitlement, health, deactivation — all the same here).
        var lapsed = await CreateAsync(unavailablePluginIds: ["crm"], from: fixture);

        var result = await lapsed.Service.GetAsync(WorkspaceKey, SurfaceKey);

        Assert.Equal("crm", result.Assignment!.PluginId);
        Assert.False(result.Assignment.IsAvailable);
    }

    private static async Task<SurfaceIdentityAssignmentFixture> CreateAsync(
        string[]? unavailablePluginIds = null,
        SurfaceIdentityAssignmentFixture? from = null)
    {
        var fixture = new SurfaceIdentityAssignmentFixture(unavailablePluginIds ?? [], from);
        if (from is null)
        {
            await fixture.SeedSurfaceAsync();
        }

        return fixture;
    }

    private sealed class SurfaceIdentityAssignmentFixture
    {
        public SurfaceIdentityAssignmentFixture(
            string[] unavailablePluginIds,
            SurfaceIdentityAssignmentFixture? from)
        {
            Workspaces = from?.Workspaces ?? new InMemoryWorkspaceManagementStore();
            Surfaces = from?.Surfaces ?? new InMemoryWorkspaceSurfaceStore();
            Installations = from?.Installations ?? new InMemoryPluginInstallationRepository();
            Sessions = from?.Sessions ?? new InMemorySurfaceSessionStore();
            Service = new SurfaceIdentityAssignmentService(
                Workspaces,
                Surfaces,
                Installations,
                new StaticPluginAvailabilityEvaluator(unavailablePluginIds),
                Sessions);
        }

        public InMemoryWorkspaceManagementStore Workspaces { get; }

        public InMemoryWorkspaceSurfaceStore Surfaces { get; }

        public InMemoryPluginInstallationRepository Installations { get; }

        public InMemorySurfaceSessionStore Sessions { get; }

        public SurfaceIdentityAssignmentService Service { get; }

        public async Task SeedSurfaceAsync()
        {
            Workspaces.AddTenant("tenant-a");
            _ = await Workspaces.UpsertAsync("tenant-a", WorkspaceKey, "Acme", "spa", isActive: true);
            _ = await Surfaces.UpsertAsync(WorkspaceKey, new WorkspaceSurfaceInput(
                SurfaceKey, "Portal", "spa", null, null, "/", SurfaceAuthentication.Public,
                null, null, null, null, null, true));
        }

        public Task InstallAsync(string pluginId, params string[] capabilities)
        {
            var installation = PluginInstallation.CreateInstalled(
                pluginId, pluginId, $"/tmp/{pluginId}.dll", null, DateTimeOffset.UtcNow);
            installation.SetCapabilities(capabilities, null, null, DateTimeOffset.UtcNow);
            return Installations.AddAsync(installation);
        }

        public Task SeedSessionAsync()
        {
            var now = DateTimeOffset.UtcNow;
            return Sessions.CreateAsync(new SurfaceSession(
                Guid.NewGuid(),
                "tenant-a",
                WorkspaceKey,
                SurfaceKey,
                "portal.example.de",
                new SurfaceSubject("crm.example", "lead-42"),
                new SurfaceIdentity(
                    "Erika Muster",
                    new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                    "password",
                    now,
                    now.AddHours(1)),
                now,
                now.AddHours(1),
                "crm",
                null));
        }
    }
}
