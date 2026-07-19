using Callora.Core.Api;
using Callora.Core.Api.OpenApi;
using Callora.Core.Application.Data.Contracts;
using Callora.Core.Application.Entitlements;
using Callora.Core.Application.Events;
using Callora.Core.Application.Events.Contracts;
using Callora.Core.Application.Extensions;
using Callora.Core.Application.Jobs;
using Callora.Core.Application.Jobs.Contracts;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Monitoring;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Secrets.Contracts;
using Callora.Core.Infrastructure.DependencyInjection;
using Callora.Core.Infrastructure.Events;
using Callora.Core.Infrastructure.Extensions;
using Callora.Core.Infrastructure.Persistence;
using Callora.Core.Infrastructure.Plugins;
using Callora.Core.Infrastructure.Security;
using Callora.Core.Infrastructure.Startup;
using Microsoft.AspNetCore.DataProtection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

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

        // R4: a CalloraException thrown from any service is rendered as an RFC 9457 problem
        // response with its stable error code; other exceptions fall through to 500.
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<Callora.Core.Infrastructure.Http.CalloraExceptionHandler>();

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
        builder.Services.AddDecoratableSingleton<Callora.Core.Application.Features.IFeatureFlagService,
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
                // Domänenneutral: ein Wildcard erfasst alle Callora-Meter — die Core-
                // Subsysteme (Callora.Core.PluginLifecycle/BackgroundJobs/Webhooks) ebenso
                // wie Plugin-Meter (z. B. Callora.Voip.Calls) —, ohne dass der Core einen
                // konkreten Plugin-Meter-Namen kennt.
                .AddMeter("Callora.*"));
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
        builder.Services.AddSingleton<IPluginPackageSignatureVerifier, ManifestSignaturePluginPackageVerifier>();
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
        builder.Services.AddSingleton<Callora.Core.Application.Configuration.Contracts.IPluginConfigReader, Callora.Core.Application.Configuration.ScopedPluginConfigReader>();
        builder.Services.AddScoped<Callora.Core.Application.Webhooks.IWebhookSubscriptionStore, EfWebhookSubscriptionStore>();
        builder.Services.AddSingleton<Callora.Core.Application.Webhooks.WebhookDispatcher>();
        builder.Services.AddDecoratableSingleton<Callora.Core.Application.Webhooks.Contracts.IWebhookEventPublisher,
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
        builder.Services.AddDecoratableSingleton<Callora.Core.Application.Notifications.Contracts.INotificationPublisher, Callora.Core.Application.Notifications.ScopedNotificationPublisher>();
        // Dekorierbarer Host-Service (PLAT-266): Plugins können den Mailversand
        // umhüllen (z. B. Suppression-Listen, Provider-Wechsel), indem sie einen
        // IServiceDecorator<IMailSender> exportieren. AddDecoratableSingleton registriert
        // die Basis plus einen generischen Per-Call-Proxy, der die Kette pro Aufruf aus
        // dem Live-Katalog komponiert (REV2 §9.2) statt sie beim ersten Resolve
        // einzufrieren (statisches §9.1-Antipattern).
        builder.Services.AddDecoratableSingleton<
            Callora.Core.Application.Mail.Contracts.IMailSender,
            Callora.Core.Infrastructure.Mail.SmtpMailSender>();
        builder.Services.AddScoped<Callora.Core.Application.Media.IMediaStore, EfMediaStore>();
        builder.Services.AddScoped<Callora.Core.Application.Workspaces.PluginWorkspaceDataPurger>();
        builder.Services.AddScoped<Callora.Core.Application.Workspaces.IWorkspaceDataPurgeService, WorkspaceDataPurgeService>();
        builder.Services.AddScoped<Callora.Core.Application.Security.IUserDataSubjectService, EfUserDataSubjectService>();
        builder.Services.AddSingleton<Callora.Core.Application.Media.IMediaStorage, Callora.Core.Infrastructure.Media.FileSystemMediaStorage>();
        builder.Services.AddSingleton<Callora.Core.Application.Media.Contracts.IMediaLibrary, Callora.Core.Application.Media.ScopedMediaLibrary>();
        builder.Services.AddSingleton<Callora.Core.Application.Migrations.Contracts.IPluginMigrationRunner, Callora.Core.Infrastructure.Plugins.ScopedPluginMigrationRunner>();
        builder.Services.AddScoped<Callora.Core.Application.CustomFields.ICustomFieldStore, EfCustomFieldStore>();
        builder.Services.AddScoped<Callora.Core.Infrastructure.CustomFields.RegistryCustomFieldSyncService>();
        builder.Services.AddScoped<IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>, PluginCustomFieldSyncSubscriber>();
        // Webhook data-minimization: domain-neutral field set — core baseline plus
        // plugin-declared "sensitiveFields" (PLAT-244). Registry is a singleton
        // because the singleton WebhookDispatcher depends on it.
        builder.Services.AddSingleton<Callora.Core.Application.Webhooks.SensitivePayloadFieldRegistry>();
        builder.Services.AddScoped<Callora.Core.Infrastructure.Webhooks.RegistrySensitiveFieldSyncService>();
        builder.Services.AddScoped<IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>, PluginSensitiveFieldSyncSubscriber>();
        builder.Services.AddScoped<Callora.Core.Application.Persistence.IPluginSchemaDropper,
            EfPluginSchemaDropper>();
        builder.Services.AddScoped<Callora.Core.Application.Persistence.IPluginDataDocumentCleaner,
            EfPluginDataDocumentCleaner>();
        builder.Services.AddScoped<IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>,
            Callora.Core.Infrastructure.Events.PluginSchemaCleanupSubscriber>();
        builder.Services.AddSingleton<Callora.Core.Application.CustomFields.Contracts.ICustomFieldAccessor, Callora.Core.Application.CustomFields.ScopedCustomFieldAccessor>();
        builder.Services.AddScoped<Callora.Core.Application.Flows.IFlowStore, EfFlowStore>();
        builder.Services.AddSingleton<Callora.Core.Application.Flows.RuleEvaluator>();
        builder.Services.AddScoped<Callora.Core.Application.Flows.FlowActionRegistry>();

        // Business-Event-Bus (PLAT-270): benannte Events, an die sich Flows,
        // Webhooks und Plugins generisch hängen. Der Bus ist auch für Plugins
        // auflösbar (PluginContract), damit sie Events publizieren können.
        builder.Services.AddSingleton<Callora.Core.Application.Events.Business.BusinessEventBus>();
        builder.Services.AddSingleton<Callora.Core.Application.Events.Contracts.IBusinessEventBus>(
            sp => sp.GetRequiredService<Callora.Core.Application.Events.Business.BusinessEventBus>());
        builder.Services.AddSingleton<Callora.Core.Application.Events.Business.BusinessEventRegistry>();
        // Host IBusinessEventProvider implementations (Workspace, User, …) are registered
        // automatically by the AddCalloraContracts assembly scan below — no manual entry.
        builder.Services.AddSingleton<CachedWorkspaceTemplateResolutionService>();
        builder.Services.AddSingleton<IWorkspaceTemplateResolutionService>(sp => sp.GetRequiredService<CachedWorkspaceTemplateResolutionService>());
        builder.Services.AddSingleton<IWorkspaceTemplateResolutionCache>(sp => sp.GetRequiredService<CachedWorkspaceTemplateResolutionService>());
        // Nach der DB-Initialisierung (AddBackendPersistence) starten, damit der
        // Worker nicht gegen noch fehlende Tabellen pollt.
        builder.Services.AddHostedService<BackgroundJobWorkerHostedService>();
        builder.Services.AddHostedService<RecurringJobSchedulerHostedService>();
        builder.Services.AddHostedService<CalloraHostStartupHostedService>();
        builder.Services.AddScoped<Callora.Core.Application.Plugins.IPluginDiscoveryService, LocalPluginDiscoveryService>();
        // Console commands (Symfony console.command equivalent): framework commands live
        // here in DI; the skeleton runner dispatches to them (REV2 §6). Plugins may export
        // their own ICalloraConsoleCommand.
        builder.Services.AddScoped<Callora.Core.Application.Cli.CalloraConsoleRunner>();
        // Auto-register every framework console command in this assembly (the Symfony
        // console.command discovery equivalent) — a new command needs no wiring here.
        // Plugin-provided commands are picked up from the plugin catalog at dispatch time.
        builder.Services.AddCalloraConsoleCommands(typeof(CalloraHostCompositionExtensions).Assembly);
        // Auto-register host contract-role implementations (job handlers, flow actions,
        // rule evaluators, event listeners/providers) — the autoconfiguration equivalent
        // (R1); plugin-provided contributors of the same roles come from the catalog.
        builder.Services.AddCalloraContracts(typeof(CalloraHostCompositionExtensions).Assembly);
        builder.Services.AddHostedService<LocalPluginDiscoveryHostedService>();
        builder.Services.AddHostedService<PluginRuntimeRehydrationHostedService>();
        builder.Services.AddHostedService<PluginUiAssetPublishHostedService>();

        return builder;
    }

    /// <summary>Wires middleware and maps every host and plugin endpoint.</summary>
    public static WebApplication MapCalloraHost(this WebApplication app)
    {
        var backendOptions = app.Services.GetRequiredService<BackendHostOptions>();

        // R4: expected domain faults (CalloraException) become RFC 9457 problems. Registered
        // first so it wraps the whole endpoint pipeline.
        app.UseExceptionHandler();

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
        // CSRF guard: cookie-authenticated state changes must originate same-origin
        // (or an explicitly allowed origin). Header-authenticated requests carry no
        // cookie and are exempt. Layered on top of the auth cookie's SameSite=Lax.
        app.UseBackendCsrfGuard(backendOptions);
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

        // Auth stays here: it is the login/token mechanism (anonymous /api/auth),
        // not an operator resource. MapControllers wires the MVC discovery (plugin
        // controllers plus the modules' application parts). Every operator /api/*
        // resource moved to Callora.Administration, the storefront to
        // Callora.Workspace; the skeleton composes both modules explicitly.
        app.MapAuthEndpoints();
        app.MapControllers();

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
