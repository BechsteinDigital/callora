using Callora.Core.Application.Security;
using Callora.Core.Tests.Support;
using Xunit;

namespace Callora.Core.Tests.Application.Security;

/// <summary>
/// The local-account controls required before a production release (#104): one
/// password policy everywhere, bounded lockout, and deactivation as the
/// non-destructive alternative to deletion.
/// </summary>
public sealed class BackendAccountControlTests
{
    private const string Subject = "operator-1";
    private const string StrongPassword = "initial-password-1";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short")]
    [InlineData("elevenchars")]
    public void PasswordPolicy_RejectsWeakPasswords(string? password)
    {
        Assert.False(BackendPasswordPolicy.IsAcceptable(password));
        Assert.NotNull(BackendPasswordPolicy.Validate(password));
    }

    [Fact]
    public void PasswordPolicy_AcceptsTheMinimumLength()
    {
        Assert.True(BackendPasswordPolicy.IsAcceptable(new string('x', BackendPasswordPolicy.MinimumLength)));
        Assert.False(BackendPasswordPolicy.IsAcceptable(new string('x', BackendPasswordPolicy.MinimumLength - 1)));
    }

    [Fact]
    public void PasswordPolicy_RejectsAbsurdLength_SoHashingCannotBeAbused()
    {
        Assert.False(BackendPasswordPolicy.IsAcceptable(new string('x', BackendPasswordPolicy.MaximumLength + 1)));
    }

    [Fact]
    public async Task RepeatedFailures_LockTheAccount_AndTheCorrectPasswordStopsWorking()
    {
        var store = await CreateStoreAsync();

        for (var attempt = 0; attempt < BackendLockoutPolicy.MaxFailedAttempts; attempt++)
        {
            Assert.Null(await store.AuthenticateAsync(Subject, "wrong-password-here"));
        }

        // The lockout is what makes guessing bounded: even the right password fails
        // until the window elapses.
        Assert.Null(await store.AuthenticateAsync(Subject, StrongPassword));
    }

    [Fact]
    public async Task SuccessfulSignIn_ClearsTheFailureCounter()
    {
        var store = await CreateStoreAsync();

        for (var attempt = 0; attempt < BackendLockoutPolicy.MaxFailedAttempts - 1; attempt++)
        {
            Assert.Null(await store.AuthenticateAsync(Subject, "wrong-password-here"));
        }

        Assert.NotNull(await store.AuthenticateAsync(Subject, StrongPassword));

        // Counter reset: the next near-miss run must start from zero again.
        for (var attempt = 0; attempt < BackendLockoutPolicy.MaxFailedAttempts - 1; attempt++)
        {
            Assert.Null(await store.AuthenticateAsync(Subject, "wrong-password-here"));
        }

        Assert.NotNull(await store.AuthenticateAsync(Subject, StrongPassword));
    }

    [Fact]
    public async Task DisabledAccount_CannotAuthenticate_ButKeepsItsData()
    {
        var store = await CreateStoreAsync();

        Assert.True(await store.SetEnabledAsync(Subject, enabled: false));

        Assert.Null(await store.AuthenticateAsync(Subject, StrongPassword));
        var user = await store.GetByExternalIdAsync(Subject);
        Assert.NotNull(user);
        Assert.True(user!.IsDisabled);
        Assert.Equal("operator@example.test", user.Email);
    }

    [Fact]
    public async Task ReEnabling_RestoresAuthentication()
    {
        var store = await CreateStoreAsync();
        await store.SetEnabledAsync(Subject, enabled: false);

        Assert.True(await store.SetEnabledAsync(Subject, enabled: true));

        Assert.NotNull(await store.AuthenticateAsync(Subject, StrongPassword));
    }

    [Fact]
    public async Task ReEnabling_ClearsAnAccumulatedLockout()
    {
        var store = await CreateStoreAsync();
        for (var attempt = 0; attempt < BackendLockoutPolicy.MaxFailedAttempts; attempt++)
        {
            Assert.Null(await store.AuthenticateAsync(Subject, "wrong-password-here"));
        }

        await store.SetEnabledAsync(Subject, enabled: false);
        await store.SetEnabledAsync(Subject, enabled: true);

        Assert.NotNull(await store.AuthenticateAsync(Subject, StrongPassword));
    }

    [Fact]
    public async Task PasswordChange_RotatesTheSecurityStamp()
    {
        var store = await CreateStoreAsync();
        var before = (await store.GetByExternalIdAsync(Subject))!.SecurityStamp;

        await store.UpsertCredentialsAsync(Subject, null, null, "replacement-password-1");

        var after = (await store.GetByExternalIdAsync(Subject))!.SecurityStamp;
        Assert.NotEqual(before, after);
        Assert.False(string.IsNullOrWhiteSpace(after));
    }

    [Fact]
    public async Task ProfileChangeWithoutPassword_KeepsTheSecurityStamp()
    {
        var store = await CreateStoreAsync();
        var before = (await store.GetByExternalIdAsync(Subject))!.SecurityStamp;

        await store.UpsertCredentialsAsync(Subject, "renamed@example.test", "Renamed", null);

        Assert.Equal(before, (await store.GetByExternalIdAsync(Subject))!.SecurityStamp);
    }

    private static async Task<InMemoryBackendUserStore> CreateStoreAsync()
    {
        var store = new InMemoryBackendUserStore();
        await store.UpsertCredentialsAsync(Subject, "operator@example.test", "Operator", StrongPassword);
        return store;
    }
}
