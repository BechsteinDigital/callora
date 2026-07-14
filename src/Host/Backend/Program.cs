using Callora.Contracts.Communication;
using Callora.Host.Backend.Api;
using Callora.Host.Backend.Application.Abstractions;
using Callora.Host.Backend.Application.Abstractions.Extensions;
using Callora.Host.Backend.Application.Abstractions.Entitlements;
using Callora.Host.Backend.Application.Abstractions.Jobs;
using Callora.Host.Backend.Application.Communication;
using Callora.Host.Backend.Application.Entitlements;
using Callora.Host.Backend.Application.Jobs;
using Callora.Host.Backend.Application.Abstractions.Events;
using Callora.Host.Backend.Application.Abstractions.Plugins;
using Callora.Host.Backend.Application.Extensions;
using Callora.Host.Backend.Application.Events;
using Callora.Host.Backend.Application.Lifecycle;
using Callora.Host.Backend.Application.Policies;
using Callora.Host.Backend.Infrastructure.DependencyInjection;
using Callora.Host.Backend.Infrastructure.Events;
using Callora.Host.Backend.Infrastructure.Extensions;
using Callora.Host.Backend.Infrastructure.Persistence;
using Callora.Host.Backend.Infrastructure.Plugins;
using Callora.Host.Backend.Infrastructure.Security;
using Callora.Host.Backend.Infrastructure.Startup;
using Callora.Host.Workspace.Api;
using Callora.Hosting.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.DataProtection;
using Callora.Host.Backend.Application.Monitoring;
using Microsoft.OpenApi.Models;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Callora.Host.PluginContracts.Application.Data;
using Callora.Host.PluginContracts.Application.Events;
using Callora.Host.PluginContracts.Application.Jobs;
using Callora.Host.PluginContracts.Application.Secrets;

var builder = WebApplication.CreateBuilder(args);

// Lifetime-Fehler (Captive Dependencies) sollen sofort beim Start auffallen.
builder.Host.UseDefaultServiceProvider(static options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddMemoryCache();

var backendOptions = new BackendHostOptions();
builder.Configuration.GetSection("BackendHost").Bind(backendOptions);
builder.Services.AddSingleton(backendOptions);
if (!string.IsNullOrWhiteSpace(backendOptions.ProblemTypeBaseUri))
{
    Callora.Host.Backend.Api.ApiProblems.TypeBaseUri = backendOptions.ProblemTypeBaseUri;
}

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("api", new OpenApiInfo
    {
        Title = "Callora Host Backend API",
        Version = "v1"
    });
    options.SwaggerDoc("workspace", new OpenApiInfo
    {
        Title = "Callora Workspace API",
        Version = "v1"
    });

    options.DocInclusionPredicate((documentName, apiDescription) =>
    {
        var relativePath = apiDescription.RelativePath ?? string.Empty;
        var normalizedPath = "/" + relativePath.TrimStart('/');
        var isWorkspaceEndpoint = normalizedPath.StartsWith("/workspace/", StringComparison.OrdinalIgnoreCase);

        return documentName switch
        {
            "workspace" => isWorkspaceEndpoint,
            "api" => !isWorkspaceEndpoint,
            _ => false
        };
    });

    options.AddSecurityDefinition(ApiKeyAuthenticationDefaults.Scheme, new OpenApiSecurityScheme
    {
        Description = $"API key required in header '{backendOptions.ApiKeyHeaderName}'.",
        Name = backendOptions.ApiKeyHeaderName,
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = ApiKeyAuthenticationDefaults.Scheme
                }
            },
            []
        }
    });
});

var hostRegistry = new ServiceCollectionHostRegistry(builder.Services);
ServiceCollectionExtensions.AddCalloraHosting(
    hostRegistry,
    configure: options =>
    {
        builder.Configuration.GetSection("CalloraHosting").Bind(options);
        options.PluginDirectory = CalloraHostingPathResolver.ResolvePluginDirectory(options.PluginDirectory);
    });

builder.Services.AddSingleton<CommunicationChannelRegistry>();
builder.Services.AddSingleton<ICommunicationChannelRegistry>(sp => sp.GetRequiredService<CommunicationChannelRegistry>());
builder.Services.AddScoped<EfPluginDataStore>();
builder.Services.AddSingleton<IPluginDataStore, ScopedPluginDataStore>();
// Plugin-eigene EF-Datenbanken (PLAT-260): Plugins bringen echte Entities +
// EF-Migrationen in ihrem eigenen Schema mit.
builder.Services.AddSingleton<Callora.Hosting.Application.Plugins.IPluginDbContextProvider,
    NpgsqlPluginDbContextProvider>();

var backgroundJobOptions = new BackgroundJobOptions();
builder.Configuration.GetSection("BackgroundJobs").Bind(backgroundJobOptions);
builder.Services.AddSingleton(backgroundJobOptions);
builder.Services.AddScoped<IBackgroundJobStore, EfBackgroundJobStore>();
builder.Services.AddScoped<BackgroundJobHandlerResolver>();
builder.Services.AddScoped<BackgroundJobProcessor>();
builder.Services.AddSingleton<IBackgroundJobQueue, ScopedBackgroundJobQueue>();
builder.Services.AddSingleton<RecurringJobEnqueuer>();

var retentionOptions = new Callora.Host.Backend.Application.Retention.RetentionOptions();
builder.Configuration.GetSection("Retention").Bind(retentionOptions);
builder.Services.AddSingleton(retentionOptions);
builder.Services.AddScoped<IBackgroundJobHandler, Callora.Host.Backend.Application.Retention.RetentionCleanupJobHandler>();
builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Jobs.IRecurringJobProvider,
    Callora.Host.Backend.Application.Retention.RetentionRecurringJobProvider>();

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
        .AddMeter(Callora.Host.Backend.Application.Webhooks.WebhookTelemetry.MeterName));
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
builder.Services.AddScoped<Callora.Host.Backend.Application.Abstractions.Plugins.IWorkspacePluginActivationStore, EfWorkspacePluginActivationStore>();
builder.Services.AddScoped<WorkspaceUiChainResolver>();
builder.Services.AddScoped<WorkspacePublicThemeResolver>();
builder.Services.AddScoped<Callora.Host.Backend.Application.Abstractions.Configuration.ISystemConfigStore, EfSystemConfigStore>();
builder.Services.AddScoped<Callora.Host.Backend.Application.Configuration.SystemConfigResolver>();
builder.Services.AddScoped<Callora.Host.Backend.Infrastructure.Configuration.RegistryConfigSchemaSyncService>();
builder.Services.AddScoped<IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>, PluginConfigSchemaSyncSubscriber>();
builder.Services.AddSingleton<Callora.Host.Backend.Infrastructure.Http.PluginApiEndpointDataSource>();
builder.Services.AddScoped<IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>,
    Callora.Host.Backend.Infrastructure.Http.PluginApiRoutingRefreshSubscriber>();
builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Configuration.IPluginConfigReader, Callora.Host.Backend.Application.Configuration.ScopedPluginConfigReader>();
builder.Services.AddScoped<Callora.Host.Backend.Application.Abstractions.Webhooks.IWebhookSubscriptionStore, EfWebhookSubscriptionStore>();
builder.Services.AddScoped<IBackgroundJobHandler, Callora.Host.Backend.Application.Webhooks.WebhookDeliveryJobHandler>();
builder.Services.AddSingleton<Callora.Host.Backend.Application.Webhooks.WebhookDispatcher>();
builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Webhooks.IWebhookEventPublisher,
    Callora.Host.Backend.Application.Webhooks.ScopedWebhookEventPublisher>();
builder.Services.AddSingleton<Callora.Host.Backend.Application.Webhooks.WebhookEgressGuard>();
builder.Services.AddHttpClient(Callora.Host.Backend.Application.Webhooks.WebhookDeliveryJobHandler.HttpClientName, client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    })
    // Redirects could re-target a validated URL into private ranges; the
    // ConnectCallback re-validates resolved addresses at connect time so a
    // changing DNS answer (rebinding) cannot bypass the egress guard.
    .ConfigurePrimaryHttpMessageHandler(sp =>
    {
        var egressGuard = sp.GetRequiredService<Callora.Host.Backend.Application.Webhooks.WebhookEgressGuard>();
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectCallback = egressGuard.ConnectAsync
        };
    });
builder.Services.AddScoped<Callora.Host.Backend.Application.Abstractions.Notifications.INotificationStore, EfNotificationStore>();
builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Notifications.INotificationPublisher, Callora.Host.Backend.Application.Notifications.ScopedNotificationPublisher>();
// Dekorierbarer Host-Service (PLAT-266): Plugins können den Mailversand
// umhüllen (z. B. Suppression-Listen, Provider-Wechsel), indem sie einen
// IServiceDecorator<IMailSender> exportieren.
builder.Services.AddSingleton<Callora.Host.Backend.Infrastructure.Mail.SmtpMailSender>();
builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Mail.IMailSender>(sp =>
    Callora.Hosting.Application.Plugins.PluginServiceDecoration.Decorate(
        (Callora.Host.PluginContracts.Application.Mail.IMailSender)sp.GetRequiredService<Callora.Host.Backend.Infrastructure.Mail.SmtpMailSender>(),
        sp.GetRequiredService<Callora.Hosting.Application.Plugins.ICalloraPluginCatalog>()));
builder.Services.AddScoped<IBackgroundJobHandler, Callora.Host.Backend.Application.Mail.MailSendJobHandler>();
builder.Services.AddScoped<Callora.Host.Backend.Application.Abstractions.Media.IMediaStore, EfMediaStore>();
builder.Services.AddScoped<Callora.Host.Backend.Application.Abstractions.Workspaces.IWorkspaceDataPurgeService, WorkspaceDataPurgeService>();
builder.Services.AddScoped<Callora.Host.Backend.Application.Abstractions.Security.IUserDataSubjectService, EfUserDataSubjectService>();
builder.Services.AddSingleton<Callora.Host.Backend.Application.Abstractions.Media.IMediaStorage, Callora.Host.Backend.Infrastructure.Media.FileSystemMediaStorage>();
builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Media.IMediaLibrary, Callora.Host.Backend.Application.Media.ScopedMediaLibrary>();
builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Migrations.IPluginMigrationRunner, Callora.Host.Backend.Infrastructure.Plugins.ScopedPluginMigrationRunner>();
builder.Services.AddScoped<Callora.Host.Backend.Application.Abstractions.CustomFields.ICustomFieldStore, EfCustomFieldStore>();
builder.Services.AddScoped<Callora.Host.Backend.Infrastructure.CustomFields.RegistryCustomFieldSyncService>();
builder.Services.AddScoped<IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>, PluginCustomFieldSyncSubscriber>();
builder.Services.AddScoped<Callora.Host.Backend.Application.Abstractions.Persistence.IPluginSchemaDropper,
    EfPluginSchemaDropper>();
builder.Services.AddScoped<IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>,
    Callora.Host.Backend.Infrastructure.Events.PluginSchemaCleanupSubscriber>();
builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.CustomFields.ICustomFieldAccessor, Callora.Host.Backend.Application.CustomFields.ScopedCustomFieldAccessor>();
builder.Services.AddScoped<Callora.Host.Backend.Application.Abstractions.Flows.IFlowStore, EfFlowStore>();
builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Flows.IRuleConditionEvaluator, Callora.Host.Backend.Application.Flows.Conditions.EventNameConditionEvaluator>();
builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Flows.IRuleConditionEvaluator, Callora.Host.Backend.Application.Flows.Conditions.DataFieldConditionEvaluator>();
builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Flows.IRuleConditionEvaluator, Callora.Host.Backend.Application.Flows.Conditions.WorkspaceKeyConditionEvaluator>();
builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Flows.IRuleConditionEvaluator, Callora.Host.Backend.Application.Flows.Conditions.TimeWindowConditionEvaluator>();
builder.Services.AddSingleton<Callora.Host.Backend.Application.Flows.RuleEvaluator>();
builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Flows.IFlowActionHandler, Callora.Host.Backend.Application.Flows.Actions.NotificationCreateActionHandler>();
builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Flows.IFlowActionHandler, Callora.Host.Backend.Application.Flows.Actions.MailSendActionHandler>();
builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Flows.IFlowActionHandler, Callora.Host.Backend.Application.Flows.Actions.WebhookSendActionHandler>();
builder.Services.AddScoped<Callora.Host.Backend.Application.Flows.FlowActionRegistry>();
builder.Services.AddScoped<IBackgroundJobHandler, Callora.Host.Backend.Application.Flows.FlowExecuteJobHandler>();

// Business-Event-Bus (PLAT-270): benannte Events, an die sich Flows,
// Webhooks und Plugins generisch hängen. Der Bus ist auch für Plugins
// auflösbar (PluginContract), damit sie Events publizieren können.
builder.Services.AddSingleton<Callora.Host.Backend.Application.Events.Business.BusinessEventBus>();
builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Events.IBusinessEventBus>(
    sp => sp.GetRequiredService<Callora.Host.Backend.Application.Events.Business.BusinessEventBus>());
builder.Services.AddSingleton<Callora.Host.Backend.Application.Events.Business.BusinessEventRegistry>();
builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Events.IBusinessEventListener,
    Callora.Host.Backend.Application.Events.Business.FlowBusinessEventListener>();
builder.Services.AddSingleton<Callora.Host.PluginContracts.Application.Events.IBusinessEventListener,
    Callora.Host.Backend.Application.Events.Business.WebhookBusinessEventListener>();
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

var app = builder.Build();

app.UseSwagger(options =>
{
    options.RouteTemplate = "swagger/{documentName}/swagger.json";
});
app.Map("/swagger/api", apiSwagger =>
{
    apiSwagger.UseSwaggerUI(options =>
    {
        options.RoutePrefix = string.Empty;
        options.SwaggerEndpoint("/swagger/api/swagger.json", "Callora Host Backend API v1");
    });
});
app.Map("/swagger/workspace", workspaceSwagger =>
{
    workspaceSwagger.UseSwaggerUI(options =>
    {
        options.RoutePrefix = string.Empty;
        options.SwaggerEndpoint("/swagger/workspace/swagger.json", "Callora Workspace API v1");
    });
});
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
    app.Services.GetRequiredService<Callora.Host.Backend.Infrastructure.Http.PluginApiEndpointDataSource>());

app.MapAuthEndpoints();
app.MapEntitlementSyncEndpoints();
app.MapJobEndpoints();
app.MapSystemConfigEndpoints();
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

await app.RunAsync();
