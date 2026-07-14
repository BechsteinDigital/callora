using Callora.Host.Backend.Application.Entitlements;
using Callora.Host.Backend.Application.Policies;
using Callora.Host.Backend.Infrastructure.Extensions;
using Microsoft.AspNetCore.Builder;

namespace Callora.Host.Backend.Tests.Support;

internal sealed class ExtensionDiscoveryTestContext(
    WebApplication app,
    HttpClient client,
    InMemoryExtensionPointRegistryStore extensionRegistry,
    InMemoryPluginExtensionRegistrationStore extensionRegistrations,
    InMemoryPluginEntitlementStore entitlements,
    InMemoryPluginInstallationRepository installations) : IAsyncDisposable
{
    public WebApplication App { get; } = app;

    public HttpClient Client { get; } = client;

    public InMemoryExtensionPointRegistryStore ExtensionRegistry { get; } = extensionRegistry;

    public InMemoryPluginExtensionRegistrationStore ExtensionRegistrations { get; } = extensionRegistrations;

    public InMemoryPluginEntitlementStore Entitlements { get; } = entitlements;

    public InMemoryPluginInstallationRepository Installations { get; } = installations;

    public ValueTask DisposeAsync() => App.DisposeAsync();
}
