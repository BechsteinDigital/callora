using Callora.Core.Application.Entitlements;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.WorkspaceAssignments;
using Callora.Core.Tests.Support;

namespace Callora.Core.Tests.Application.Plugins.WorkspaceAssignments;

internal sealed record WorkspacePluginAssignmentServiceTestFixture(
    WorkspacePluginAssignmentService Service,
    ConfigurablePluginLifecycleService Lifecycle,
    InMemoryPluginEntitlementStore Entitlements,
    InMemoryWorkspacePluginActivationStore Activations);
