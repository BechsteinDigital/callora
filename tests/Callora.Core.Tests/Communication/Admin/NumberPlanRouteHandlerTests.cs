using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Accounts;
using Callora.Plugin.Communication.Application.Admin.Numbers;
using Callora.Plugin.Communication.Domain.Accounts;
using Xunit;

namespace Callora.Core.Tests.Communication.Admin;

/// <summary>
/// The number plan: one screen that says, per number a workspace can be reached on, which line
/// delivers it, how many of that line's calls it may hold, and what has been happening on it.
/// Configuring "the support number, at most five lines" meant two plugins and a screen that did not
/// exist.
/// </summary>
public sealed class NumberPlanRouteHandlerTests
{
    private const string Workspace = "ws-a";
    private const string Number = "+493012345678";

    // ── Reading the plan ────────────────────────────────────────────────────

    [Fact]
    public async Task EveryNumberOfTheWorkspace_IsListedWithItsLine()
    {
        var handler = new GetNumberPlanRouteHandler(
            new FakeCatalog((Number, "a1", "Berlin Trunk")), Accounts(), History());

        var response = await handler.HandleAsync(Request("GET", Workspace));

        var entry = Assert.Single(Assert.IsType<NumberPlanEntry[]>(response.Payload));
        Assert.Equal((Number, "a1", "Berlin Trunk"), (entry.Number, entry.ChannelId, entry.ChannelDisplayName));
    }

    [Fact]
    public async Task ANumberWithAQuota_ShowsIt()
    {
        var handler = new GetNumberPlanRouteHandler(
            new FakeCatalog((Number, "a1", "Berlin Trunk")),
            Accounts(Account("a1", quotas: [new CallQuota(Number, 5)])),
            History());

        var entry = Assert.Single(Assert.IsType<NumberPlanEntry[]>((await handler.HandleAsync(Request("GET", Workspace))).Payload));

        Assert.Equal(5, entry.MaxConcurrentCalls);
    }

    [Fact]
    public async Task ANumberWithoutAQuota_IsUnlimitedRatherThanZero()
    {
        // Keine Konfiguration heißt unbegrenzt — 0 anzuzeigen wäre die genaue Umkehrung.
        var handler = new GetNumberPlanRouteHandler(
            new FakeCatalog((Number, "a1", "Berlin Trunk")), Accounts(Account("a1")), History());

        var entry = Assert.Single(Assert.IsType<NumberPlanEntry[]>((await handler.HandleAsync(Request("GET", Workspace))).Payload));

        Assert.Null(entry.MaxConcurrentCalls);
    }

    [Fact]
    public async Task AQuotaWrittenInAnotherForm_StillBelongsToTheNumber()
    {
        // Der Betreiber trägt ein, wie sein Anbieter druckt; der Katalog meldet, wie das Konto es hält.
        var handler = new GetNumberPlanRouteHandler(
            new FakeCatalog((Number, "a1", "Berlin Trunk")),
            Accounts(Account("a1", quotas: [new CallQuota("0049 30 1234-5678", 3)])),
            History());

        var entry = Assert.Single(Assert.IsType<NumberPlanEntry[]>((await handler.HandleAsync(Request("GET", Workspace))).Payload));

        Assert.Equal(3, entry.MaxConcurrentCalls);
    }

    [Fact]
    public async Task WhatHappenedOnTheNumber_IsCounted()
    {
        // Die erste Frage an eine geteilte Leitung: Kam auf dieser Nummer überhaupt etwas an?
        var handler = new GetNumberPlanRouteHandler(
            new FakeCatalog((Number, "a1", "Berlin Trunk")),
            Accounts(),
            History(
                Call("c1", Number, DateTimeOffset.UnixEpoch),
                Call("c2", "0049 30 12345678", DateTimeOffset.UnixEpoch.AddHours(2)),
                Call("c3", "+493087654321", DateTimeOffset.UnixEpoch.AddHours(3))));

        var entry = Assert.Single(Assert.IsType<NumberPlanEntry[]>((await handler.HandleAsync(Request("GET", Workspace))).Payload));

        Assert.Equal(2, entry.RecentCalls);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddHours(2), entry.LastCallAt);
    }

    [Fact]
    public async Task ANumberNothingHappenedOn_SaysSoRatherThanNothing()
    {
        var handler = new GetNumberPlanRouteHandler(
            new FakeCatalog((Number, "a1", "Berlin Trunk")), Accounts(), History());

        var entry = Assert.Single(Assert.IsType<NumberPlanEntry[]>((await handler.HandleAsync(Request("GET", Workspace))).Payload));

        Assert.Equal(0, entry.RecentCalls);
        Assert.Null(entry.LastCallAt);
    }

    [Fact]
    public async Task WithoutAWorkspace_NothingIsListed()
    {
        var handler = new GetNumberPlanRouteHandler(new FakeCatalog(), Accounts(), History());

        Assert.Equal(400, (await handler.HandleAsync(Request("GET", workspaceKey: null))).StatusCode);
    }

    // ── Setting a quota ─────────────────────────────────────────────────────

    [Fact]
    public async Task AQuotaIsWrittenOntoTheLineThatCarriesTheNumber()
    {
        var accounts = Accounts(Account("a1"));
        var handler = new SetNumberQuotaRouteHandler(accounts, reconciler: null);

        var response = await handler.HandleAsync(
            Request("POST", Workspace, new { channelId = "a1", number = Number, maxConcurrentCalls = 5 }));

        Assert.Equal(200, response.StatusCode);
        var quota = Assert.Single((await accounts.GetAsync(Workspace, "a1"))!.CallQuotas);
        Assert.Equal(5, quota.MaxConcurrentCalls);
    }

    [Fact]
    public async Task ChangingAQuota_LeavesTheOtherNumbersAlone()
    {
        // Das Kontingent-Feld am Konto trägt alle Nummern; eine zu setzen darf die anderen nicht
        // löschen, sonst nimmt ein Klick unbemerkt eine andere Grenze weg.
        var accounts = Accounts(Account("a1", quotas:
        [
            new CallQuota(Number, 5),
            new CallQuota("+493087654321", 2),
            new CallQuota("crm", 10),
        ]));
        var handler = new SetNumberQuotaRouteHandler(accounts, reconciler: null);

        await handler.HandleAsync(
            Request("POST", Workspace, new { channelId = "a1", number = Number, maxConcurrentCalls = 9 }));

        var quotas = (await accounts.GetAsync(Workspace, "a1"))!.CallQuotas;
        Assert.Equal(3, quotas.Count);
        Assert.Equal(9, quotas.Single(q => PhoneNumberFormat.Normalize(q.Origin) == PhoneNumberFormat.Normalize(Number)).MaxConcurrentCalls);
        Assert.Equal(10, quotas.Single(q => q.Origin == "crm").MaxConcurrentCalls);
    }

    [Fact]
    public async Task ClearingAQuota_MakesTheNumberUnlimitedAgain()
    {
        var accounts = Accounts(Account("a1", quotas: [new CallQuota(Number, 5)]));
        var handler = new SetNumberQuotaRouteHandler(accounts, reconciler: null);

        var response = await handler.HandleAsync(
            Request("POST", Workspace, new { channelId = "a1", number = Number, maxConcurrentCalls = (int?)null }));

        Assert.Equal(200, response.StatusCode);
        Assert.Empty((await accounts.GetAsync(Workspace, "a1"))!.CallQuotas);
    }

    [Fact]
    public async Task AQuotaBelowOne_IsRefused()
    {
        // Null Leitungen ist kein Kontingent, sondern ein Verbot — und dafür gibt es das Konto selbst.
        var handler = new SetNumberQuotaRouteHandler(Accounts(Account("a1")), reconciler: null);

        var response = await handler.HandleAsync(
            Request("POST", Workspace, new { channelId = "a1", number = Number, maxConcurrentCalls = 0 }));

        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task ALineOfAnotherWorkspace_IsNotFound()
    {
        var accounts = Accounts(Account("b1", workspaceKey: "ws-b"));
        var handler = new SetNumberQuotaRouteHandler(accounts, reconciler: null);

        var response = await handler.HandleAsync(
            Request("POST", Workspace, new { channelId = "b1", number = Number, maxConcurrentCalls = 5 }));

        Assert.Equal(404, response.StatusCode);
    }

    // ── Helper ──────────────────────────────────────────────────────────────

    private static SipAccount Account(string id, string workspaceKey = Workspace, CallQuota[]? quotas = null) =>
        new(
            id,
            workspaceKey,
            $"Trunk {id}",
            new SipConnection("sip.example.com", 5060, SipTransport.Udp, SipAccountMode.Trunk,
                IpAuthentication.Instance, registrationExpirySeconds: null),
            maxConcurrentCalls: 10,
            enabled: true,
            quotas);

    private static InMemoryAccounts Accounts(params SipAccount[] accounts) => new(accounts);

    private static FakeHistory History(params CallHistoryEntry[] calls) => new(calls);

    private static CallHistoryEntry Call(string id, string localIdentity, DateTimeOffset startedAt) =>
        new(id, "Inbound", "+4917012345678", localIdentity, startedAt, null, null, 0, "Completed", null, []);

    private static HostAdminApiRequest Request(string method, string? workspaceKey, object? body = null) =>
        new(
            "communication",
            method,
            "numbers",
            new Dictionary<string, string>(),
            new Dictionary<string, string[]>(),
            body is null ? null : JsonSerializer.SerializeToElement(body),
            UserId: "user-1",
            WorkspaceKey: workspaceKey);

    private sealed class FakeCatalog(params (string Number, string ChannelId, string ChannelDisplayName)[] numbers)
        : IInboundNumberCatalog
    {
        public Task<IReadOnlyList<InboundNumber>> ListAsync(
            string workspaceKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InboundNumber>>(
                [.. numbers.Select(n => new InboundNumber(n.Number, n.ChannelId, n.ChannelDisplayName))]);
    }

    private sealed class FakeHistory(CallHistoryEntry[] calls) : ICallHistory
    {
        public Task<IReadOnlyList<CallHistoryEntry>> ListRecentAsync(
            string workspaceKey, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CallHistoryEntry>>([.. calls]);
    }

    private sealed class InMemoryAccounts(SipAccount[] seeded) : ISipAccountStore
    {
        private readonly List<SipAccount> _accounts = [.. seeded];

        public Task<IReadOnlyList<SipAccount>> ListAsync(string workspaceKey, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SipAccount>>([.. _accounts.Where(a => a.WorkspaceKey == workspaceKey)]);

        public Task<IReadOnlyList<SipAccount>> ListEnabledAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SipAccount>>([.. _accounts]);

        public Task<SipAccount?> GetAsync(string workspaceKey, string accountId, CancellationToken ct = default) =>
            Task.FromResult(_accounts.FirstOrDefault(a => a.WorkspaceKey == workspaceKey && a.Id == accountId));

        public Task AddAsync(SipAccount account, CancellationToken ct = default)
        {
            _accounts.Add(account);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(SipAccount account, CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> DeleteAsync(string workspaceKey, string accountId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<int> DeleteByWorkspaceAsync(string workspaceKey, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
