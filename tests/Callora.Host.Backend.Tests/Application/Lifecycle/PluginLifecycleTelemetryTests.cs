using System.Diagnostics;
using System.Diagnostics.Metrics;
using Callora.Host.Backend.Application.Abstractions;
using Callora.Host.Backend.Application.Lifecycle;
using Callora.Host.Backend.Application.Policies;
using Callora.Host.Backend.Tests.Support;
using VoipHost.PluginContracts.Application.Plugins;

namespace Callora.Host.Backend.Tests.Application.Lifecycle;

public sealed class PluginLifecycleTelemetryTests
{
    [Fact]
    public async Task LifecycleOperations_EmitTracingAndMetrics_WithCorrelationTags()
    {
        var lifecycle = new FakeHostPluginLifecycle
        {
            InstallResult = new HostPluginOperationResult(HostPluginOperation.Install, true, "plugin-telemetry", null),
            ActivateResult = new HostPluginOperationResult(HostPluginOperation.Activate, true, "plugin-telemetry", null),
            DeactivateResult = new HostPluginOperationResult(HostPluginOperation.Deactivate, true, "plugin-telemetry", null),
            UninstallResult = new HostPluginOperationResult(HostPluginOperation.Uninstall, true, "plugin-telemetry", null)
        };

        var sut = new PluginLifecycleService(
            lifecycle,
            new StaticPluginActivationPolicy(PluginActivationDecision.Allow()),
            new InMemoryPluginEntitlementStore(new BackendHostOptions()),
            new InMemoryHostAuditStore(),
            new InMemoryPluginInstallationRepository(),
            new NoOpHostUnitOfWork(),
            new StaticPluginPackageRegistryReader(),
            new StaticPluginPackageSignatureVerifier(),
            new StaticNuGetPluginAssemblyResolver(),
            new RecordingHostApplicationEventPublisher());

        var activities = new List<Activity>();
        using var activityListener = CreateActivityListener(activities);
        ActivitySource.AddActivityListener(activityListener);

        var metricMeasurements = new List<(string InstrumentName, string Action, string Scope, string Outcome, string CorrelationId)>();
        using var meterListener = CreateMeterListener(metricMeasurements);

        await sut.InstallAsync(new InstallPluginCommand("/tmp/plugin-telemetry.dll", null, "tester"));
        await sut.ActivateAsync(new PluginLifecycleCommand("plugin-telemetry", "tester", "workspace-a"));
        await sut.DeactivateAsync(new PluginLifecycleCommand("plugin-telemetry", "tester", "workspace-a"));
        await sut.UninstallAsync(new PluginLifecycleCommand("plugin-telemetry", "tester", "workspace-a"));

        activityListener.Dispose();
        meterListener.Dispose();

        Assert.Contains(activities, x => x.OperationName == "plugin.lifecycle.install");
        Assert.Contains(activities, x => x.OperationName == "plugin.lifecycle.activate");
        Assert.Contains(activities, x => x.OperationName == "plugin.lifecycle.deactivate");
        Assert.Contains(activities, x => x.OperationName == "plugin.lifecycle.uninstall");

        Assert.Contains(activities, x =>
            x.OperationName == "plugin.lifecycle.install" &&
            x.GetTagItem("plugin.lifecycle.outcome")?.ToString() == "success" &&
            !string.IsNullOrWhiteSpace(x.GetTagItem("correlation.id")?.ToString()));
        Assert.Contains(activities, x =>
            x.OperationName == "plugin.lifecycle.activate" &&
            x.GetTagItem("plugin.lifecycle.scope")?.ToString() == "workspace" &&
            x.GetTagItem("workspace.key")?.ToString() == "workspace-a" &&
            x.GetTagItem("plugin.lifecycle.outcome")?.ToString() == "success" &&
            !string.IsNullOrWhiteSpace(x.GetTagItem("correlation.id")?.ToString()));
        Assert.Contains(activities, x =>
            x.OperationName == "plugin.lifecycle.deactivate" &&
            x.GetTagItem("plugin.lifecycle.scope")?.ToString() == "workspace" &&
            x.GetTagItem("workspace.key")?.ToString() == "workspace-a" &&
            x.GetTagItem("plugin.lifecycle.outcome")?.ToString() == "success" &&
            !string.IsNullOrWhiteSpace(x.GetTagItem("correlation.id")?.ToString()));
        Assert.Contains(activities, x =>
            x.OperationName == "plugin.lifecycle.uninstall" &&
            x.GetTagItem("plugin.lifecycle.outcome")?.ToString() == "success" &&
            !string.IsNullOrWhiteSpace(x.GetTagItem("correlation.id")?.ToString()));

        Assert.Contains(metricMeasurements, x =>
            x.InstrumentName == PluginLifecycleTelemetry.OperationCountMetricName &&
            x.Action == "install" &&
            x.Scope == "global" &&
            x.Outcome == "success");
        Assert.Contains(metricMeasurements, x =>
            x.InstrumentName == PluginLifecycleTelemetry.OperationCountMetricName &&
            x.Action == "activate" &&
            x.Scope == "workspace" &&
            x.Outcome == "success");
        Assert.Contains(metricMeasurements, x =>
            x.InstrumentName == PluginLifecycleTelemetry.OperationCountMetricName &&
            x.Action == "deactivate" &&
            x.Scope == "workspace" &&
            x.Outcome == "success");
        Assert.Contains(metricMeasurements, x =>
            x.InstrumentName == PluginLifecycleTelemetry.OperationCountMetricName &&
            x.Action == "uninstall" &&
            x.Scope == "workspace" &&
            x.Outcome == "success");

        Assert.Contains(metricMeasurements, x =>
            x.InstrumentName == PluginLifecycleTelemetry.DurationMetricName &&
            x.Action == "install" &&
            x.Scope == "global" &&
            x.Outcome == "success");
        Assert.Contains(metricMeasurements, x =>
            x.InstrumentName == PluginLifecycleTelemetry.DurationMetricName &&
            x.Action == "activate" &&
            x.Scope == "workspace" &&
            x.Outcome == "success");
        Assert.Contains(metricMeasurements, x =>
            x.InstrumentName == PluginLifecycleTelemetry.DurationMetricName &&
            x.Action == "deactivate" &&
            x.Scope == "workspace" &&
            x.Outcome == "success");
        Assert.Contains(metricMeasurements, x =>
            x.InstrumentName == PluginLifecycleTelemetry.DurationMetricName &&
            x.Action == "uninstall" &&
            x.Scope == "workspace" &&
            x.Outcome == "success");

        Assert.All(metricMeasurements, x => Assert.False(string.IsNullOrWhiteSpace(x.CorrelationId)));
    }

    private static ActivityListener CreateActivityListener(List<Activity> activities)
    {
        return new ActivityListener
        {
            ShouldListenTo = source => source.Name == PluginLifecycleTelemetry.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = static (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                lock (activities)
                {
                    activities.Add(activity);
                }
            }
        };
    }

    private static MeterListener CreateMeterListener(
        List<(string InstrumentName, string Action, string Scope, string Outcome, string CorrelationId)> metricMeasurements)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == PluginLifecycleTelemetry.MeterName)
                    meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            if (TryReadTags(tags, out var action, out var scope, out var outcome, out var correlationId))
                metricMeasurements.Add((instrument.Name, action, scope, outcome, correlationId));
        });
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) =>
        {
            if (TryReadTags(tags, out var action, out var scope, out var outcome, out var correlationId))
                metricMeasurements.Add((instrument.Name, action, scope, outcome, correlationId));
        });

        listener.Start();
        return listener;
    }

    private static bool TryReadTags(
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        out string action,
        out string scope,
        out string outcome,
        out string correlationId)
    {
        action = string.Empty;
        scope = string.Empty;
        outcome = string.Empty;
        correlationId = string.Empty;

        foreach (var tag in tags)
        {
            if (tag.Key == "plugin.lifecycle.action")
            {
                action = tag.Value?.ToString() ?? string.Empty;
            }

            if (tag.Key == "plugin.lifecycle.scope")
            {
                scope = tag.Value?.ToString() ?? string.Empty;
            }

            if (tag.Key == "plugin.lifecycle.outcome")
            {
                outcome = tag.Value?.ToString() ?? string.Empty;
            }

            if (tag.Key == "correlation.id")
            {
                correlationId = tag.Value?.ToString() ?? string.Empty;
            }
        }

        return !string.IsNullOrWhiteSpace(action) &&
               !string.IsNullOrWhiteSpace(scope) &&
               !string.IsNullOrWhiteSpace(outcome) &&
               !string.IsNullOrWhiteSpace(correlationId);
    }
}
