using Callora.Core.Application.Entitlements;
using Callora.Core.Application.Policies;
using Callora.Core.Infrastructure.Extensions;
using Microsoft.AspNetCore.Builder;

namespace Callora.Core.Tests.Support;

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
