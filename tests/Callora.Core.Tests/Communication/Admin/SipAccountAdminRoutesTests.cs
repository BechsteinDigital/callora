using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Secrets.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Accounts;
using Callora.Plugin.Communication.Application.Admin;
using Callora.Plugin.Communication.Application.Admin.SipAccounts;
using Callora.Plugin.Communication.Domain.Accounts;
using Xunit;

namespace Callora.Core.Tests.Communication.Admin;

/// <summary>
/// SIP-account operator routes (B6-2): create/list/get/enable/disable/delete over the workspace-scoped
/// store. Focus: credentials are protected on create and never echoed; the workspace comes from the
/// token (HostAdminApiRequest.WorkspaceKey), isolating one workspace from another; missing scope is
/// rejected; not-found paths return 404.
/// </summary>
public sealed class SipAccountAdminRoutesTests
{
    private const string PluginId = "communication";

    [Fact]
    public async Task Create_ProtectsPassword_PersistsRefNeverPlaintext_AndDoesNotEchoIt()
    {
        var store = new InMemorySipAccountStore();
        var protector = new CapturingDataProtector();
        var handler = new CreateSipAccountRouteHandler(store, protector, PluginId);

        var response = await handler.HandleAsync(Request("POST", "sip-accounts", "ws-a", body: new
        {
            displayName = "Alice",
            host = "sip.example.com",
            port = 5060,
            transport = "Udp",
            username = "alice",
            password = "s3cret",
        }));

        Assert.Equal(201, response.StatusCode);

        var persisted = Assert.Single(await store.ListAsync("ws-a"));
        var digest = Assert.IsType<DigestAuthentication>(persisted.Connection.Authentication);
        Assert.NotEqual("s3cret", digest.PasswordSecretRef); // a reference is stored, never the plaintext
        Assert.True(protector.TryUnprotect(PluginId, digest.PasswordSecretRef, out var recovered));
        Assert.Equal("s3cret", recovered); // ...and it resolves back to the password (it was protected)
        Assert.Equal("ws-a", persisted.WorkspaceKey);

        // The response projection has no password field at all; confirm the serialized body omits it.
        var json = JsonSerializer.Serialize(response.Payload);
        Assert.DoesNotContain("s3cret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_WithoutWorkspace_IsRejected()
    {
        var handler = new CreateSipAccountRouteHandler(new InMemorySipAccountStore(), new CapturingDataProtector(), PluginId);

        var response = await handler.HandleAsync(Request("POST", "sip-accounts", workspaceKey: null, body: new
        {
            displayName = "Alice",
            host = "sip.example.com",
            username = "alice",
            password = "s3cret",
        }));

        Assert.Equal(400, response.StatusCode); // no bound workspace and no explicit target
    }

    [Fact]
    public async Task Create_MissingRequiredField_IsRejected()
    {
        var handler = new CreateSipAccountRouteHandler(new InMemorySipAccountStore(), new CapturingDataProtector(), PluginId);

        var response = await handler.HandleAsync(Request("POST", "sip-accounts", "ws-a", body: new
        {
            host = "sip.example.com",
            username = "alice",
            password = "s3cret",
            // displayName missing
        }));

        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task PlatformOperator_TargetsWorkspaceViaQuery()
    {
        var store = new InMemorySipAccountStore();
        var handler = new CreateSipAccountRouteHandler(store, new CapturingDataProtector(), PluginId);

        // No bound workspace (platform operator) → explicit ?workspaceKey= is honoured.
        var response = await handler.HandleAsync(Request("POST", "sip-accounts", workspaceKey: null,
            query: new() { ["workspaceKey"] = ["ws-target"] },
            body: new { displayName = "Alice", host = "h", username = "alice", password = "s3cret" }));

        Assert.Equal(201, response.StatusCode);
        Assert.Single(await store.ListAsync("ws-target"));
    }

    [Fact]
    public async Task BoundWorkspace_IgnoresQueryOverride()
    {
        var store = new InMemorySipAccountStore();
        var handler = new CreateSipAccountRouteHandler(store, new CapturingDataProtector(), PluginId);

        // A workspace-scoped caller's token wins; a spoofed ?workspaceKey= must not redirect the write.
        await handler.HandleAsync(Request("POST", "sip-accounts", "ws-a",
            query: new() { ["workspaceKey"] = ["ws-other"] },
            body: new { displayName = "Alice", host = "h", username = "alice", password = "s3cret" }));

        Assert.Single(await store.ListAsync("ws-a"));
        Assert.Empty(await store.ListAsync("ws-other"));
    }

    [Fact]
    public async Task List_ReturnsOnlyCallersWorkspace()
    {
        var store = new InMemorySipAccountStore();
        store.Seed(Account("a1", "ws-a"));
        store.Seed(Account("b1", "ws-b"));
        var handler = new ListSipAccountsRouteHandler(store);

        var response = await handler.HandleAsync(Request("GET", "sip-accounts", "ws-a"));

        Assert.Equal(200, response.StatusCode);
        var accounts = Assert.IsType<SipAccountResponse[]>(response.Payload);
        Assert.Equal("a1", Assert.Single(accounts).Id);
    }

    [Fact]
    public async Task Get_ForeignWorkspaceAccount_IsNotFound()
    {
        var store = new InMemorySipAccountStore();
        store.Seed(Account("b1", "ws-b"));
        var handler = new GetSipAccountRouteHandler(store);

        // Caller bound to ws-a must not read ws-b's account, even by its exact id.
        var response = await handler.HandleAsync(
            Request("GET", "sip-accounts/b1", "ws-a", routeValues: new() { ["accountId"] = "b1" }));

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task EnableDisable_TogglesEnabledAndStatus()
    {
        var store = new InMemorySipAccountStore();
        store.Seed(Account("a1", "ws-a", enabled: false));
        var enable = new SetSipAccountEnabledRouteHandler(store, enabled: true);
        var disable = new SetSipAccountEnabledRouteHandler(store, enabled: false);
        var route = new Dictionary<string, string> { ["accountId"] = "a1" };

        var enabled = await enable.HandleAsync(Request("POST", "sip-accounts/a1/enable", "ws-a", routeValues: route));
        Assert.Equal(200, enabled.StatusCode);
        var afterEnable = await store.GetAsync("ws-a", "a1");
        Assert.True(afterEnable!.Enabled);
        Assert.Equal(SipAccountStatus.Connecting, afterEnable.Status);

        var disabled = await disable.HandleAsync(Request("POST", "sip-accounts/a1/disable", "ws-a", routeValues: route));
        Assert.Equal(200, disabled.StatusCode);
        var afterDisable = await store.GetAsync("ws-a", "a1");
        Assert.False(afterDisable!.Enabled);
        Assert.Equal(SipAccountStatus.Disabled, afterDisable.Status);
    }

    [Fact]
    public async Task Enable_MissingAccount_IsNotFound()
    {
        var handler = new SetSipAccountEnabledRouteHandler(new InMemorySipAccountStore(), enabled: true);

        var response = await handler.HandleAsync(
            Request("POST", "sip-accounts/nope/enable", "ws-a", routeValues: new() { ["accountId"] = "nope" }));

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesAccount_ThenIsNotFound()
    {
        var store = new InMemorySipAccountStore();
        store.Seed(Account("a1", "ws-a"));
        var handler = new DeleteSipAccountRouteHandler(store);
        var route = new Dictionary<string, string> { ["accountId"] = "a1" };

        var deleted = await handler.HandleAsync(Request("DELETE", "sip-accounts/a1", "ws-a", routeValues: route));
        Assert.Equal(204, deleted.StatusCode);
        Assert.Empty(await store.ListAsync("ws-a"));

        var again = await handler.HandleAsync(Request("DELETE", "sip-accounts/a1", "ws-a", routeValues: route));
        Assert.Equal(404, again.StatusCode);
    }

    [Theory]
    [InlineData(0, 300, 1)]      // port too low
    [InlineData(70000, 300, 1)]  // port too high
    [InlineData(5060, 0, 1)]      // registration expiry too low
    [InlineData(5060, 300, 0)]    // max concurrent calls too low
    public async Task Create_InvalidNumericField_IsRejected(int port, int expiry, int maxCalls)
    {
        var handler = new CreateSipAccountRouteHandler(new InMemorySipAccountStore(), new CapturingDataProtector(), PluginId);

        var response = await handler.HandleAsync(Request("POST", "sip-accounts", "ws-a", body: new
        {
            displayName = "Alice",
            host = "sip.example.com",
            username = "alice",
            password = "s3cret",
            port,
            registrationExpirySeconds = expiry,
            maxConcurrentCalls = maxCalls,
        }));

        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Create_NonObjectBody_IsRejected()
    {
        var handler = new CreateSipAccountRouteHandler(new InMemorySipAccountStore(), new CapturingDataProtector(), PluginId);

        // A JSON array is not a create object — must surface as 400, never a 500.
        var response = await handler.HandleAsync(Request("POST", "sip-accounts", "ws-a", body: new[] { 1, 2, 3 }));

        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Mutation_ForeignWorkspaceAccount_IsNotFound_AndUntouched()
    {
        var store = new InMemorySipAccountStore();
        store.Seed(Account("b1", "ws-b"));
        var route = new Dictionary<string, string> { ["accountId"] = "b1" };

        // A ws-a caller must not enable or delete ws-b's account, even by its exact id.
        var enable = await new SetSipAccountEnabledRouteHandler(store, enabled: true)
            .HandleAsync(Request("POST", "sip-accounts/b1/enable", "ws-a", routeValues: route));
        Assert.Equal(404, enable.StatusCode);

        var deleted = await new DeleteSipAccountRouteHandler(store)
            .HandleAsync(Request("DELETE", "sip-accounts/b1", "ws-a", routeValues: route));
        Assert.Equal(404, deleted.StatusCode);

        Assert.Single(await store.ListAsync("ws-b")); // ws-b's account is untouched
    }

    private static SipAccount Account(string id, string workspaceKey, bool enabled = true)
    {
        var auth = new DigestAuthentication("user", authId: null, passwordSecretRef: "ref");
        var connection = new SipConnection("sip.example.com", 5060, SipTransport.Udp, SipAccountMode.Register, auth, 300);
        return new SipAccount(id, workspaceKey, $"Account {id}", connection, maxConcurrentCalls: 1, enabled);
    }

    private static HostAdminApiRequest Request(
        string method,
        string path,
        string? workspaceKey,
        Dictionary<string, string>? routeValues = null,
        object? body = null,
        Dictionary<string, string[]>? query = null)
    {
        JsonElement? bodyElement = body is null ? null : JsonSerializer.SerializeToElement(body);
        return new HostAdminApiRequest(
            PluginId,
            method,
            path,
            routeValues ?? new Dictionary<string, string>(),
            query ?? new Dictionary<string, string[]>(),
            bodyElement,
            UserId: "user-1",
            WorkspaceKey: workspaceKey);
    }

    private sealed class CapturingDataProtector : IPluginDataProtector
    {
        private readonly Dictionary<string, string> _protected = new(StringComparer.Ordinal);

        public string Protect(string pluginId, string plaintext)
        {
            // A reference that does NOT embed the plaintext, so tests can prove the plaintext is not stored.
            var reference = $"protected:{_protected.Count}";
            _protected[reference] = plaintext;
            return reference;
        }

        public bool TryUnprotect(string pluginId, string protectedValue, out string plaintext) =>
            _protected.TryGetValue(protectedValue, out plaintext!);
    }

    private sealed class InMemorySipAccountStore : ISipAccountStore
    {
        private readonly List<SipAccount> _accounts = [];

        public void Seed(SipAccount account) => _accounts.Add(account);

        public Task<IReadOnlyList<SipAccount>> ListAsync(string workspaceKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SipAccount>>(
                _accounts.Where(a => a.WorkspaceKey == workspaceKey).ToArray());

        public Task<IReadOnlyList<SipAccount>> ListEnabledAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SipAccount>>(_accounts.Where(a => a.Enabled).ToArray());

        public Task<SipAccount?> GetAsync(string workspaceKey, string accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_accounts.FirstOrDefault(a => a.WorkspaceKey == workspaceKey && a.Id == accountId));

        public Task AddAsync(SipAccount account, CancellationToken cancellationToken = default)
        {
            _accounts.Add(account);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(SipAccount account, CancellationToken cancellationToken = default)
        {
            _accounts.RemoveAll(a => a.WorkspaceKey == account.WorkspaceKey && a.Id == account.Id);
            _accounts.Add(account);
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(string workspaceKey, string accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_accounts.RemoveAll(a => a.WorkspaceKey == workspaceKey && a.Id == accountId) > 0);

        public Task<int> DeleteByWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(_accounts.RemoveAll(a => a.WorkspaceKey == workspaceKey));
    }
}
