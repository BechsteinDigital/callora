using Callora.Host.Backend.Api;
using Callora.Host.Backend.Application.Abstractions;
using Callora.Host.Backend.Application.Abstractions.Events;
using Callora.Host.Backend.Application.Abstractions.Plugins;
using Callora.Host.Backend.Application.Events;
using Callora.Host.Backend.Application.Lifecycle;
using Callora.Host.Backend.Application.Policies;
using Callora.Host.Backend.Infrastructure.DependencyInjection;
using Callora.Host.Backend.Infrastructure.Events;
using Callora.Host.Backend.Infrastructure.Persistence;
using Callora.Host.Backend.Infrastructure.Plugins;
using Callora.Host.Backend.Infrastructure.Security;
using Callora.Host.Backend.Infrastructure.Startup;
using Callora.Hosting.Infrastructure.DependencyInjection;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();

var backendOptions = new BackendHostOptions();
builder.Configuration.GetSection("BackendHost").Bind(backendOptions);
builder.Services.AddSingleton(backendOptions);

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Callora Host Backend API",
        Version = "v1"
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
    configure: options => builder.Configuration.GetSection("CalloraHosting").Bind(options));

builder.Services.AddSingleton<IPluginActivationPolicy, AllowlistPluginActivationPolicy>();
builder.Services.AddSingleton<IPluginPackageRegistryReader, JsonPluginPackageRegistryReader>();
builder.Services.AddSingleton<INuGetPluginAssemblyResolver, LocalNuGetPackagePluginAssemblyResolver>();
builder.Services.AddScoped<IHostApplicationEventPublisher, HostApplicationEventPublisher>();
builder.Services.AddScoped<IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>, PluginLifecycleLoggingSubscriber>();
builder.Services.AddBackendPersistence(backendOptions);
builder.Services.AddBackendApiSecurity(backendOptions);
builder.Services.AddScoped<IPluginLifecycleService, PluginLifecycleService>();
builder.Services.AddHostedService<CalloraHostStartupHostedService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPluginEndpoints();

await app.RunAsync();
