using Callora.Core.Api;
using Callora.Core.Application.Extensions;
using Callora.Core.Application.Entitlements;
using Callora.Core.Application.Jobs;
using Callora.Core.Application.Events;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Policies;
using Callora.Core.Infrastructure.DependencyInjection;
using Callora.Core.Infrastructure.Events;
using Callora.Core.Infrastructure.Extensions;
using Callora.Core.Infrastructure.Persistence;
using Callora.Core.Infrastructure.Plugins;
using Callora.Core.Infrastructure.Security;
using Callora.Core.Infrastructure.Startup;
using Callora.Host.Workspace.Api;
using Microsoft.AspNetCore.DataProtection;
using Callora.Core.Application.Monitoring;
using Callora.Core.Api.OpenApi;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Callora.Host.PluginContracts.Application.Data;
using Callora.Host.PluginContracts.Application.Events;
using Callora.Host.PluginContracts.Application.Jobs;
using Callora.Host.PluginContracts.Application.Secrets;

namespace Callora.Core.Infrastructure.DependencyInjection;

/// <summary>
/// The Callora host composition root, extracted from Program.cs so the thin
/// distribution skeleton only calls AddCalloraHost + MapCalloraHost (REV2 §12
/// Phase 2, step 1). Same registrations, order and middleware as before.
/// </summary>
public static class CalloraHostCompositionExtensions
{
    /// <summary>Registers every host service, option and hosted worker.</summary>
    public static WebApplicationBuilder AddCalloraHost(this WebApplicationBuilder builder)
    {
        // Lifetime-Fehler (Captive Dependencies) sollen sofort beim Start auffallen.
        builder.Host.UseDefaultServiceProvider(static options =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        });

        builder.Services.AddEndpointsApiExplorer();
        // MVC controllers run alongside the remaining minimal-API endpoints during the
        // endpoint-to-controller migration (CODE_STRUCTURE_RULES.md, Phase C).
        builder.Services.AddControllers();
        builder.Services.AddMemoryCache();

        var backendOptions = new BackendHostOptions();
        builder.Configuration.GetSection("BackendHost").Bind(backendOptions);
        builder.Services.AddSingleton(backendOptions);
        if (!string.IsNullOrWhiteSpace(backendOptions.ProblemTypeBaseUri))
        {
            Callora.Core.Api.ApiProblems.TypeBaseUri = backendOptions.ProblemTypeBaseUri;
        }

        builder.Services.AddBackendOpenApi();

        var hostRegistry = new ServiceCollectionHostRegistry(builder.Services);
        ServiceCollectionExtensions.AddCalloraHosting(
            hostRegistry,
            configure: options =>
            {
                builder.Configuration.GetSection("CalloraHosting").Bind(options);
                options.PluginDirectory = CalloraHostingPathResolver.ResolvePluginDirectory(options.PluginDirectory);
            });

        // Zentrale Feature-Flags (PLAT-263), aus BackendHost:FeatureFlags.
        builder.Services.AddSingleton<Callora.Core.Application.Features.IFeatureFlagService,
            Callora.Core.Infrastructure.Features.ConfiguredFeatureFlagService>();
        builder.Services.AddScoped<EfPluginDataStore>();
        builder.Services.AddSingleton<IPluginDataStore, ScopedPluginDataStore>();
        // Plugin-eigene EF-Datenbanken (PLAT-260): Plugins bringen echte Entities +
        // EF-Migrationen in ihrem eigenen Schema mit.
        builder.Services.AddSingleton<Callora.Core.Application.Plugins.IPluginDbContextProvider,
            NpgsqlPluginDbContextProvider>();

        var backgroundJobOptions = new BackgroundJobOptions();
        builder.Configuration.GetSection("BackgroundJobs").Bind(backgroundJobOptions);
        builder.Services.AddSingleton(backgroundJobOptions);
        builder.Services.AddScoped<IBackgroundJobStore, EfBackgroundJobStore>();
        builder.Services.AddScoped<BackgroundJobHandlerResolver>();
        builder.Services.AddScoped<BackgroundJobProcessor>();
        builder.Services.AddSingleton<IBackgroundJobQueue, ScopedBackgroundJobQueue>();
        builder.Services.AddSingleton<RecurringJobEnqueuer>();

        var retentionOptions = new Callora.Core.Application.Retention.RetentionOptions();
        builder.Configuration.GetSection("Retention").Bind(retentionOptions);
        builder.Services.AddSingleton(retentionOptions);
        builder.Services.AddScoped<IBackgroundJobHandler, Callora.Core.Application.Retention.RetentionCleanupJobHandler>();
        builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Jobs.IRecurringJobProvider,
            Callora.Core.Application.Retention.RetentionRecurringJobProvider>();

        builder.Services.AddSingleton<ISecretStore>(sp => new ChainedSecretStore(
        [
            new EnvironmentSecretStore(),
            new ConfigurationSecretStore(builder.Configuration)
        ]));
        builder.Services.AddDataProtection()
            .SetApplicationName("callora-host")
            // Datenbank-Keyring: mehrinstanzfähig; Alt-Keys aus dem Dateisystem
            // importiert der DB-Init-Service einmalig (PLAT-232).
            .PersistKeysToDbContext<HostPersistenceDbContext>();
        builder.Services.AddSingleton<IPluginDataProtector, DataProtectionPluginDataProtector>();
        builder.Services.AddScoped<IMarketplaceEntitlementEventStore, EfMarketplaceEntitlementEventStore>();
        builder.Services.AddScoped<MarketplaceEntitlementApplier>();
        builder.Services.AddScoped<IBackgroundJobHandler, MarketplaceEntitlementSyncJobHandler>();

        var observabilityOptions = new ObservabilityOptions();
        builder.Configuration.GetSection("Observability").Bind(observabilityOptions);
        builder.Services.AddSingleton(observabilityOptions);
        var openTelemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(observabilityOptions.ServiceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddSource(PluginLifecycleTelemetry.ActivitySourceName))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter(PluginLifecycleTelemetry.MeterName)
                .AddMeter(BackgroundJobTelemetry.MeterName)
                // Call-Metriken kommen aus dem Voice-Plugin (PLAT-257); Meter-Name als
                // Literal, damit der Host das Plugin nicht referenziert.
                .AddMeter("Callora.Voip.Calls")
                .AddMeter(Callora.Core.Application.Webhooks.WebhookTelemetry.MeterName));
        if (!string.IsNullOrWhiteSpace(observabilityOptions.OtlpEndpoint))
        {
            openTelemetry.UseOtlpExporter(
                OpenTelemetry.Exporter.OtlpExportProtocol.Grpc,
                new Uri(observabilityOptions.OtlpEndpoint));
        }

        builder.Services.AddHealthChecks()
            .AddCheck<DatabaseReadinessHealthCheck>("database");
        builder.Services.AddScoped<IPluginActivationPolicy, AllowlistPluginActivationPolicy>();
        builder.Services.AddScoped<IPluginEntitlementStore, EfPluginEntitlementStore>();
        builder.Services.AddSingleton<IExtensionPointRegistryStore, InMemoryExtensionPointRegistryStore>();
        builder.Services.AddSingleton<IPluginExtensionRegistrationStore, InMemoryPluginExtensionRegistrationStore>();
        builder.Services.AddSingleton<IPluginPackageRegistryReader, JsonPluginPackageRegistryReader>();
        builder.Services.AddSingleton<IPluginSignatureTrustStore, ConfiguredPluginSignatureTrustStore>();
        builder.Services.AddSingleton<IPluginPackageSignatureVerifier, AuthenticodePluginPackageSignatureVerifier>();
        builder.Services.AddSingleton<INuGetPluginAssemblyResolver, LocalNuGetPackagePluginAssemblyResolver>();
        builder.Services.AddSingleton<ILocalPluginProjectBuilder, LocalPluginProjectBuilder>();
        builder.Services.AddSingleton<ILocalPluginInstallSourceResolver, LocalPluginInstallSourceResolver>();
        builder.Services.AddScoped<IPluginUiAssetPublisher, PluginUiAssetPublisher>();
        builder.Services.AddScoped<IHostApplicationEventDispatcher, HostApplicationEventDispatcher>();
        builder.Services.AddScoped<IHostApplicationEventPublisher, HostApplicationEventPublisher>();
        builder.Services.AddScoped<IHostEventPublisher>(sp => sp.GetRequiredService<IHostApplicationEventPublisher>());
        builder.Services.AddScoped<IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>, PluginLifecycleLoggingSubscriber>();
        builder.Services.AddScoped<IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>, ThemeJsonWorkspaceTemplateSyncSubscriber>();
        builder.Services.AddScoped<IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>, PluginUiAssetPublishSubscriber>();
        builder.Services.AddScoped<IThemeJsonWorkspaceTemplateSyncService, ThemeJsonWorkspaceTemplateSyncService>();
        builder.Services.AddBackendPersistence(backendOptions);
        builder.Services.AddBackendApiSecurity(backendOptions);
        builder.Services.AddBackendRateLimiting(backendOptions);
        builder.Services.AddScoped<IPluginLifecycleService, PluginLifecycleService>();
        builder.Services.AddScoped<IWorkspacePluginActivationReader, EfWorkspacePluginActivationReader>();
        builder.Services.AddScoped<Callora.Core.Application.Plugins.IWorkspacePluginActivationStore, EfWorkspacePluginActivationStore>();
        builder.Services.AddScoped<Callora.Core.Application.Lifecycle.PluginCapabilityGuard>();
        builder.Services.AddScoped<Callora.Core.Application.Plugins.PluginAvailabilityEvaluator>();
        builder.Services.AddScoped<Callora.Core.Application.Plugins.IPluginAvailabilityEvaluator>(
            static sp => sp.GetRequiredService<Callora.Core.Application.Plugins.PluginAvailabilityEvaluator>());
        builder.Services.AddScoped<WorkspaceUiChainResolver>();
        builder.Services.AddScoped<WorkspacePublicThemeResolver>();
        builder.Services.AddScoped<Callora.Core.Application.Configuration.ISystemConfigStore, EfSystemConfigStore>();
        builder.Services.AddScoped<Callora.Core.Application.Configuration.SystemConfigResolver>();
        builder.Services.AddScoped<Callora.Core.Infrastructure.Configuration.RegistryConfigSchemaSyncService>();
        builder.Services.AddScoped<IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>, PluginConfigSchemaSyncSubscriber>();
        builder.Services.AddSingleton<Callora.Core.Infrastructure.Http.PluginApiEndpointDataSource>();
        builder.Services.AddScoped<IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>,
            Callora.Core.Infrastructure.Http.PluginApiRoutingRefreshSubscriber>();
        builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Configuration.IPluginConfigReader, Callora.Core.Application.Configuration.ScopedPluginConfigReader>();
        builder.Services.AddScoped<Callora.Core.Application.Webhooks.IWebhookSubscriptionStore, EfWebhookSubscriptionStore>();
        builder.Services.AddScoped<IBackgroundJobHandler, Callora.Core.Application.Webhooks.WebhookDeliveryJobHandler>();
        builder.Services.AddSingleton<Callora.Core.Application.Webhooks.WebhookDispatcher>();
        builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Webhooks.IWebhookEventPublisher,
            Callora.Core.Application.Webhooks.ScopedWebhookEventPublisher>();
        builder.Services.AddSingleton<Callora.Core.Application.Webhooks.WebhookEgressGuard>();
        builder.Services.AddHttpClient(Callora.Core.Application.Webhooks.WebhookDeliveryJobHandler.HttpClientName, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            // Redirects could re-target a validated URL into private ranges; the
            // ConnectCallback re-validates resolved addresses at connect time so a
            // changing DNS answer (rebinding) cannot bypass the egress guard.
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var egressGuard = sp.GetRequiredService<Callora.Core.Application.Webhooks.WebhookEgressGuard>();
                return new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    ConnectCallback = egressGuard.ConnectAsync
                };
            });
        builder.Services.AddScoped<Callora.Core.Application.Notifications.INotificationStore, EfNotificationStore>();
        builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Notifications.INotificationPublisher, Callora.Core.Application.Notifications.ScopedNotificationPublisher>();
        // Dekorierbarer Host-Service (PLAT-266): Plugins können den Mailversand
        // umhüllen (z. B. Suppression-Listen, Provider-Wechsel), indem sie einen
        // IServiceDecorator<IMailSender> exportieren.
        builder.Services.AddSingleton<Callora.Core.Infrastructure.Mail.SmtpMailSender>();
        builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Mail.IMailSender>(sp =>
            Callora.Core.Application.Plugins.PluginServiceDecoration.Decorate(
                (Callora.Host.PluginContracts.Application.Mail.IMailSender)sp.GetRequiredService<Callora.Core.Infrastructure.Mail.SmtpMailSender>(),
                sp.GetRequiredService<Callora.Core.Application.Plugins.ICalloraPluginCatalog>()));
        builder.Services.AddScoped<IBackgroundJobHandler, Callora.Core.Application.Mail.MailSendJobHandler>();
        builder.Services.AddScoped<Callora.Core.Application.Media.IMediaStore, EfMediaStore>();
        builder.Services.AddScoped<Callora.Core.Application.Workspaces.PluginWorkspaceDataPurger>();
        builder.Services.AddScoped<Callora.Core.Application.Workspaces.IWorkspaceDataPurgeService, WorkspaceDataPurgeService>();
        builder.Services.AddScoped<Callora.Core.Application.Security.IUserDataSubjectService, EfUserDataSubjectService>();
        builder.Services.AddSingleton<Callora.Core.Application.Media.IMediaStorage, Callora.Core.Infrastructure.Media.FileSystemMediaStorage>();
        builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Media.IMediaLibrary, Callora.Core.Application.Media.ScopedMediaLibrary>();
        builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Migrations.IPluginMigrationRunner, Callora.Core.Infrastructure.Plugins.ScopedPluginMigrationRunner>();
        builder.Services.AddScoped<Callora.Core.Application.CustomFields.ICustomFieldStore, EfCustomFieldStore>();
        builder.Services.AddScoped<Callora.Core.Infrastructure.CustomFields.RegistryCustomFieldSyncService>();
        builder.Services.AddScoped<IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>, PluginCustomFieldSyncSubscriber>();
        builder.Services.AddScoped<Callora.Core.Application.Persistence.IPluginSchemaDropper,
            EfPluginSchemaDropper>();
        builder.Services.AddScoped<Callora.Core.Application.Persistence.IPluginDataDocumentCleaner,
            EfPluginDataDocumentCleaner>();
        builder.Services.AddScoped<IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>,
            Callora.Core.Infrastructure.Events.PluginSchemaCleanupSubscriber>();
        builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.CustomFields.ICustomFieldAccessor, Callora.Core.Application.CustomFields.ScopedCustomFieldAccessor>();
        builder.Services.AddScoped<Callora.Core.Application.Flows.IFlowStore, EfFlowStore>();
        builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Flows.IRuleConditionEvaluator, Callora.Core.Application.Flows.Conditions.EventNameConditionEvaluator>();
        builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Flows.IRuleConditionEvaluator, Callora.Core.Application.Flows.Conditions.DataFieldConditionEvaluator>();
        builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Flows.IRuleConditionEvaluator, Callora.Core.Application.Flows.Conditions.WorkspaceKeyConditionEvaluator>();
        builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Flows.IRuleConditionEvaluator, Callora.Core.Application.Flows.Conditions.TimeWindowConditionEvaluator>();
        builder.Services.AddSingleton<Callora.Core.Application.Flows.RuleEvaluator>();
        builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Flows.IFlowActionHandler, Callora.Core.Application.Flows.Actions.NotificationCreateActionHandler>();
        builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Flows.IFlowActionHandler, Callora.Core.Application.Flows.Actions.MailSendActionHandler>();
        builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Flows.IFlowActionHandler, Callora.Core.Application.Flows.Actions.WebhookSendActionHandler>();
        builder.Services.AddScoped<Callora.Core.Application.Flows.FlowActionRegistry>();
        builder.Services.AddScoped<IBackgroundJobHandler, Callora.Core.Application.Flows.FlowExecuteJobHandler>();

        // Business-Event-Bus (PLAT-270): benannte Events, an die sich Flows,
        // Webhooks und Plugins generisch hängen. Der Bus ist auch für Plugins
        // auflösbar (PluginContract), damit sie Events publizieren können.
        builder.Services.AddSingleton<Callora.Core.Application.Events.Business.BusinessEventBus>();
        builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Events.IBusinessEventBus>(
            sp => sp.GetRequiredService<Callora.Core.Application.Events.Business.BusinessEventBus>());
        builder.Services.AddSingleton<Callora.Core.Application.Events.Business.BusinessEventRegistry>();
        builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Events.IBusinessEventListener,
            Callora.Core.Application.Events.Business.FlowBusinessEventListener>();
        builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Events.IBusinessEventListener,
            Callora.Core.Application.Events.Business.WebhookBusinessEventListener>();
        builder.Services.AddSingleton<CachedWorkspaceTemplateResolutionService>();
        builder.Services.AddSingleton<IWorkspaceTemplateResolutionService>(sp => sp.GetRequiredService<CachedWorkspaceTemplateResolutionService>());
        builder.Services.AddSingleton<IWorkspaceTemplateResolutionCache>(sp => sp.GetRequiredService<CachedWorkspaceTemplateResolutionService>());
        // Nach der DB-Initialisierung (AddBackendPersistence) starten, damit der
        // Worker nicht gegen noch fehlende Tabellen pollt.
        builder.Services.AddHostedService<BackgroundJobWorkerHostedService>();
        builder.Services.AddHostedService<RecurringJobSchedulerHostedService>();
        builder.Services.AddHostedService<CalloraHostStartupHostedService>();
        builder.Services.AddHostedService<LocalPluginDiscoveryHostedService>();
        builder.Services.AddHostedService<PluginRuntimeRehydrationHostedService>();
        builder.Services.AddHostedService<PluginUiAssetPublishHostedService>();

        return builder;
    }

    /// <summary>Wires middleware and maps every host and plugin endpoint.</summary>
    public static WebApplication MapCalloraHost(this WebApplication app)
    {
        var backendOptions = app.Services.GetRequiredService<BackendHostOptions>();

        app.MapBackendOpenApi();
        app.Use(async (context, next) =>
        {
            // Browsers must not MIME-sniff plugin assets or media streams.
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            await next();
        });
        app.UseStaticFiles();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        // Liveness (/health, vom Frontdoor geprobt): Prozess antwortet, keine
        // Abhängigkeitsprüfung. Readiness (/ready) prüft die Datenbank.
        // Der JSON-Body ist Vertrag: die Workspace-Shell prüft status == "ok".
        app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = static (context, _) =>
            {
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync("{\"status\":\"ok\"}");
            }
        });
        app.MapHealthChecks("/ready");
        // Plugin-Controller-Routen (Shopware-artige Discovery, PLAT-257): Plugins
        // liefern ihre API-Routen selbst; Aktivieren fügt hinzu, Deaktivieren entfernt.
        ((IEndpointRouteBuilder)app).DataSources.Add(
            app.Services.GetRequiredService<Callora.Core.Infrastructure.Http.PluginApiEndpointDataSource>());

        app.MapAuthEndpoints();
        app.MapEntitlementSyncEndpoints();
        app.MapJobEndpoints();
        app.MapSystemConfigEndpoints();
        app.MapFeatureEndpoints();
        app.MapWebhookEndpoints();
        app.MapBusinessEventEndpoints();
        app.MapNotificationEndpoints();
        app.MapMediaEndpoints();
        app.MapCustomFieldEndpoints();
        app.MapFlowEndpoints();
        app.MapPluginEndpoints();
        app.MapPluginAssetEndpoints(backendOptions);
        app.MapPluginAdminExtensionEndpoints();
        app.MapThemeEndpoints();
        app.MapRbacEndpoints();
        app.MapControllers();
        if (backendOptions.EnableTenantManagementApi)
        {
            app.MapTenantEndpoints();
        }
        app.MapUserEndpoints();
        app.MapWorkspaceEndpoints();
        app.MapWorkspaceThemeEndpoints();
        app.MapWorkspacePublicEndpoints();

        // Dev-Defaults (JWT-Key, Demo-Admin-Passwort, DB-Passwort, Bootstrap-API-Key)
        // dürfen eine Produktionsumgebung nie erreichen: außerhalb Development wird der
        // Start verweigert, in Development bleibt es bei einer lauten Warnung.
        var secretViolations = BackendSecretHygiene.Inspect(backendOptions);
        if (secretViolations.Count > 0)
        {
            if (app.Environment.IsDevelopment())
            {
                foreach (var violation in secretViolations)
                {
                    app.Logger.LogWarning("SECURITY: {Violation}", violation);
                }
            }
            else
            {
                throw new InvalidOperationException(
                    "Refusing to start: insecure development defaults are active outside Development:" +
                    Environment.NewLine + "- " + string.Join(Environment.NewLine + "- ", secretViolations));
            }
        }

        return app;
    }
}
