using Callora.Core.Application.Entitlements;
using Callora.Core.Application.Flows;
using Callora.Core.Application.Mail;
using Callora.Core.Application.Retention;
using Callora.Core.Application.Webhooks;
using Callora.Core.Extensibility;

namespace Callora.Core.Tests.Application.Jobs;

/// <summary>
/// Locks the R7 security decision: every host-infrastructure job handler is
/// [HostProtected], so a plugin cannot silently override it under plugin-wins.
/// </summary>
public sealed class HostProtectedHandlersTests
{
    [Theory]
    [InlineData(typeof(RetentionCleanupJobHandler))]
    [InlineData(typeof(MarketplaceEntitlementSyncJobHandler))]
    [InlineData(typeof(FlowExecuteJobHandler))]
    [InlineData(typeof(MailSendJobHandler))]
    [InlineData(typeof(WebhookDeliveryJobHandler))]
    public void HostInfrastructureHandler_IsHostProtected(Type handlerType)
    {
        Assert.True(
            handlerType.IsDefined(typeof(HostProtectedAttribute), inherit: false),
            $"{handlerType.Name} must be [HostProtected] (host-infrastructure handler, R7).");
    }
}
