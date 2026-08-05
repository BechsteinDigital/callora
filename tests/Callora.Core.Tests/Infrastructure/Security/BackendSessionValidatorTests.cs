using Callora.Core.Application.Security;
using Callora.Core.Infrastructure.Security;
using Callora.Core.Tests.Support;
using System.Security.Claims;
using Xunit;

namespace Callora.Core.Tests.Infrastructure.Security;

/// <summary>
/// A stolen token must stop working the moment the account it belongs to changes
/// (#105). Each test takes a session that was valid, applies one revocation event,
/// and asserts the same session is rejected afterwards.
/// </summary>
public sealed class BackendSessionValidatorTests
{
    private const string Subject = "operator-1";
    private const string StrongPassword = "initial-password-1";

    [Fact]
    public async Task ValidSession_IsAccepted()
    {
        var (validator, store, _) = await CreateAsync();
        var session = await IssueSessionAsync(store);

        Assert.Null(await validator.ValidateAsync(session));
    }

    [Fact]
    public async Task PasswordChange_RevokesTheSession()
    {
        var (validator, store, _) = await CreateAsync();
        var session = await IssueSessionAsync(store);

        await store.UpsertCredentialsAsync(Subject, null, null, "replacement-password-1");

        Assert.NotNull(await validator.ValidateAsync(session));
    }

    [Fact]
    public async Task Deactivation_RevokesTheSession()
    {
        var (validator, store, _) = await CreateAsync();
        var session = await IssueSessionAsync(store);

        await store.SetEnabledAsync(Subject, enabled: false);

        Assert.NotNull(await validator.ValidateAsync(session));
    }

    [Fact]
    public async Task Deletion_RevokesTheSession()
    {
        var (validator, store, _) = await CreateAsync();
        var session = await IssueSessionAsync(store);

        await store.RemoveAsync(Subject);

        Assert.NotNull(await validator.ValidateAsync(session));
    }

    [Fact]
    public async Task AuthorizationChange_RevokesTheSession()
    {
        var (validator, store, _) = await CreateAsync();
        var session = await IssueSessionAsync(store);

        await store.RevokeSessionsAsync(Subject);

        Assert.NotNull(await validator.ValidateAsync(session));
    }

    [Fact]
    public async Task Logout_RevokesOnlyThatSession()
    {
        var (validator, store, revocations) = await CreateAsync();
        var first = await IssueSessionAsync(store, tokenId: "session-1");
        var second = await IssueSessionAsync(store, tokenId: "session-2");

        await revocations.RevokeAsync("session-1", DateTimeOffset.UtcNow.AddHours(1));

        Assert.NotNull(await validator.ValidateAsync(first));
        Assert.Null(await validator.ValidateAsync(second));
    }

    [Fact]
    public async Task ForeignToken_WithoutSecurityStamp_IsLeftAlone()
    {
        var (validator, _, _) = await CreateAsync();

        // An external OIDC token or a named integration credential: not a session
        // this host minted, so it carries no stamp and is not ours to revoke.
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "someone-else")],
            authenticationType: "Test"));

        Assert.Null(await validator.ValidateAsync(principal));
    }

    [Fact]
    public async Task ReEnabling_DoesNotResurrectPreDeactivationSessions()
    {
        var (validator, store, _) = await CreateAsync();
        var session = await IssueSessionAsync(store);

        await store.SetEnabledAsync(Subject, enabled: false);
        await store.SetEnabledAsync(Subject, enabled: true);

        Assert.NotNull(await validator.ValidateAsync(session));
    }

    /// <summary>Builds the principal the login endpoint would issue right now.</summary>
    private static async Task<ClaimsPrincipal> IssueSessionAsync(
        IBackendUserStore store,
        string tokenId = "session-1")
    {
        var user = await store.GetByExternalIdAsync(Subject);
        Assert.NotNull(user);

        return new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", Subject),
                new Claim(BackendClaimTypes.SecurityStamp, user!.SecurityStamp),
                new Claim(BackendClaimTypes.TokenId, tokenId)
            ],
            authenticationType: "Test"));
    }

    private static async Task<(IBackendSessionValidator Validator, IBackendUserStore Store, IBackendSessionRevocationStore Revocations)>
        CreateAsync()
    {
        var store = new InMemoryBackendUserStore();
        await store.UpsertCredentialsAsync(Subject, "operator@example.test", "Operator", StrongPassword);

        var revocations = new InMemorySessionRevocationStore();
        var cache = new BackendSessionStateCache();
        // The production decorator drops the cached account on every stamp rotation,
        // which is exactly what makes revocation immediate rather than eventual.
        var cachingStore = new SessionStateInvalidatingUserStore(store, cache);
        var validator = new BackendSessionValidator(cachingStore, revocations, cache);
        return (validator, cachingStore, revocations);
    }
}
