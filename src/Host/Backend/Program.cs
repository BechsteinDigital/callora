using Callora.Contracts.Communication;
using Callora.Host.Backend.Api;
using Callora.Host.Backend.Application.Abstractions;
using Callora.Host.Backend.Application.Abstractions.Extensions;
using Callora.Host.Backend.Application.Abstractions.Entitlements;
using Callora.Host.Backend.Application.Abstractions.Jobs;
using Callora.Host.Backend.Application.Communication;
using Callora.Host.Backend.Application.Communication.Calls;
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
builder.Services.AddSingleton<CallEventBroadcaster>();
builder.Services.AddSingleton<ActiveCallRegistry>();
builder.Services.AddSingleton<CallPlacementService>();
builder.Services.AddHostedService<IncomingCallMonitor>();
builder.Services.AddScoped<EfPluginDataStore>();
builder.Services.AddSingleton<IPluginDataStore, ScopedPluginDataStore>();

var backgroundJobOptions = new BackgroundJobOptions();
builder.Configuration.GetSection("BackgroundJobs").Bind(backgroundJobOptions);
builder.Services.AddSingleton(backgroundJobOptions);
builder.Services.AddScoped<IBackgroundJobStore, EfBackgroundJobStore>();
builder.Services.AddScoped<BackgroundJobHandlerResolver>();
builder.Services.AddScoped<BackgroundJobProcessor>();
builder.Services.AddSingleton<IBackgroundJobQueue, ScopedBackgroundJobQueue>();
builder.Services.AddSingleton<RecurringJobEnqueuer>();

builder.Services.AddSingleton<ISecretStore>(sp => new ChainedSecretStore(
[
    new EnvironmentSecretStore(),
    new ConfigurationSecretStore(builder.Configuration)
]));
builder.Services.AddDataProtection().SetApplicationName("callora-host");
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
        .AddSource(PluginLifecycleTelemetry.ActivitySourceName))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddMeter(PluginLifecycleTelemetry.MeterName)
        .AddMeter(BackgroundJobTelemetry.MeterName));
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
builder.Services.AddScoped<IPluginLifecycleService, PluginLifecycleService>();
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
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapHealthChecks("/ready");
app.MapAuthEndpoints();
app.MapEntitlementSyncEndpoints();
app.MapJobEndpoints();
app.MapCallEndpoints();
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

await app.RunAsync();
