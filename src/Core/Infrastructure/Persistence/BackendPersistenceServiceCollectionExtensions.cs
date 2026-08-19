using Callora.Core.Application.Audit;
using Callora.Core.Application.Extensions;
using Callora.Core.Application.Integrations;
using Callora.Core.Application.Persistence;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;
using Callora.Core.Application.Tenants;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Security;
using Callora.Core.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Callora.Core.Infrastructure.Persistence;

public static class BackendPersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Obergrenze für eine einzelne Datenbankanweisung im Anfragepfad.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nicht „vorher gab es keine": Npgsql bringt 30 Sekunden mit. Nur war das ein Wert, den
    /// niemand gewählt hatte, und für eine Web-Anfrage ist er zu hoch — der öffentliche
    /// Renderpfad setzt mehrere Abfragen hintereinander ab, und schon zwei davon in der Vorgabe
    /// überschreiten jede Geduld auf der anderen Seite. Wer wartet, ist nicht der Host, sondern
    /// der Besucher.
    /// </para>
    /// <para>
    /// Zehn Sekunden liegen weit über jeder gesunden Abfrage hier und weit unter dem, was ein
    /// Browser abwartet. Eine Abfrage, die sie reißt, ist kein langsamer Normalfall, sondern eine
    /// blockierte Verbindung — und die soll abbrechen, solange der Fehler noch bei ihr steht.
    /// Migrationen laufen ausdrücklich nicht hierunter, siehe
    /// <see cref="DbContextMigrationExtensions.MigrationCommandTimeout"/>.
    /// </para>
    /// </remarks>
    public static readonly TimeSpan RequestCommandTimeout = TimeSpan.FromSeconds(10);

    public static IServiceCollection AddBackendPersistence(
        this IServiceCollection services,
        BackendHostOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DatabaseConnectionString);

        services.AddDbContext<HostPersistenceDbContext>(db =>
            db.UseNpgsql(
                options.DatabaseConnectionString,
                npgsql => npgsql.CommandTimeout((int)RequestCommandTimeout.TotalSeconds)));

        // Trägt den Workspace-Scope des Requests in den globalen Query-Filter
        // des DbContext (PLAT-267). Operatoren/Nicht-Requests umgehen ihn.
        services.AddHttpContextAccessor();
        services.AddScoped<IWorkspaceScopeContext, HttpWorkspaceScopeContext>();

        // Das Repository löst gespeicherte Plugin-Pfade gegen die konfigurierten Wurzeln auf
        // (#307). Im Host bringt AddCalloraHosting die Übersetzung mit den echten Wurzeln schon
        // mit — TryAdd greift dann nicht. Wer nur die Persistenz registriert, bekommt hier die
        // Vorgabewerte, statt an einer fehlenden Registrierung zu scheitern.
        services.TryAddSingleton<Callora.Core.Application.Plugins.IPluginAssemblyPathPortability>(
            provider => new Plugins.PluginAssemblyPathPortability(
                provider.GetService<Callora.Core.Application.Options.CalloraHostingOptions>()
                ?? new Callora.Core.Application.Options.CalloraHostingOptions()));
        services.AddScoped<IPluginInstallationRepository, EfPluginInstallationRepository>();
        services.AddScoped<IPluginAuditLogRepository, EfPluginAuditLogRepository>();
        // Oberflächentexte: die Abweichungen aus dem Admin (#273, ADR-024). Die Basis kommt aus
        // den Paketen und braucht keinen Speicher.
        services.AddScoped<Callora.Core.Application.Snippets.ISnippetOverrideStore, EfSnippetOverrideStore>();
        services.AddScoped<EfSnippetBaseStore>();
        services.AddScoped<Callora.Core.Application.Snippets.ISnippetBaseStore>(
            provider => provider.GetRequiredService<EfSnippetBaseStore>());
        services.AddScoped<Callora.Core.Application.Snippets.ISnippetBaseSource>(
            provider => provider.GetRequiredService<EfSnippetBaseStore>());
        services.AddScoped<Callora.Core.Application.Snippets.SnippetResolver>();
        services.AddScoped<Callora.Core.Application.Snippets.SnippetAdminService>();
        // Der Cache lebt als Singleton über die Anfragen hinweg und holt sich den inneren
        // Resolver je Auflösung aus einem eigenen Scope — sonst hinge der DbContext an ihm fest.
        services.AddSingleton<Callora.Core.Application.Snippets.CachedSnippetResolver>();
        services.AddSingleton<Callora.Core.Application.Snippets.ISnippetResolver>(
            provider => provider.GetRequiredService<Callora.Core.Application.Snippets.CachedSnippetResolver>());
        services.AddSingleton<Callora.Core.Application.Snippets.ISnippetCache>(
            provider => provider.GetRequiredService<Callora.Core.Application.Snippets.CachedSnippetResolver>());
        // Der Katalog gehört der Anfrage: einmal asynchron geladen, danach synchron gelesen.
        services.AddScoped<Callora.Core.Application.Snippets.ISnippetCatalog,
            Callora.Core.Application.Snippets.SnippetCatalog>();
        // Die Factory als SINGLETON, obwohl der Katalog scoped ist. ASP.NET löst sie beim
        // Aufbau der MvcOptions aus der Wurzel des Containers auf; scoped registriert nahm sie
        // den ganzen Host mit — "Cannot consume scoped service 'ISnippetCatalog' from singleton
        // 'IOptions<MvcOptions>'", Ende mit Code 134, bevor ein Port offen war. Drei Tage lang
        // unbemerkt, weil dotnet watch den Absturz auffängt und der Container weiter "Up" meldet.
        //
        // Den Katalog liefert ein Delegat, der ihn aus den Diensten DER LAUFENDEN ANFRAGE holt.
        // Nicht aus einem eigenen Scope: Der Katalog wird einmal je Anfrage geladen und danach
        // synchron gelesen — ein zweiter Scope wäre ein zweiter Ladevorgang und könnte andere
        // Texte liefern als der Rest derselben Anfrage sieht.
        services.AddSingleton<Microsoft.Extensions.Localization.IStringLocalizerFactory>(provider =>
        {
            var accessor = provider.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            return new Callora.Core.Application.Snippets.SnippetStringLocalizerFactory(
                () => accessor.HttpContext?.RequestServices
                    .GetService<Callora.Core.Application.Snippets.ISnippetCatalog>());
        });
        // Direkt injizierbar bleibt der Localizer scoped: Wer ihn so bezieht, steht bereits in
        // einer Anfrage und bekommt deren Katalog ohne Umweg über den Accessor.
        services.AddScoped<Microsoft.Extensions.Localization.IStringLocalizer>(
            provider => new Callora.Core.Application.Snippets.SnippetStringLocalizer(
                provider.GetRequiredService<Callora.Core.Application.Snippets.ISnippetCatalog>));
        services.AddScoped<IHostUnitOfWork, EfHostUnitOfWork>();
        services.AddScoped<IBackendRbacStore, EfBackendRbacStore>();
        services.AddScoped<IIntegrationCredentialStore, EfIntegrationCredentialStore>();
        // Session revocation (#105): a durable revocation list, the bounded
        // account-state cache the request-path validator reads, and the decorator
        // that drops a cached account the moment its stamp rotates.
        services.AddScoped<EfBackendUserStore>();
        services.AddScoped<IBackendUserStore>(provider => new SessionStateInvalidatingUserStore(
            provider.GetRequiredService<EfBackendUserStore>(),
            provider.GetRequiredService<BackendSessionStateCache>()));
        services.AddScoped<IBackendSessionRevocationStore, EfBackendSessionRevocationStore>();
        services.AddSingleton<BackendSessionStateCache>();
        services.AddScoped<IBackendSessionValidator, BackendSessionValidator>();
        services.AddScoped<ITenantManagementStore, EfTenantManagementStore>();
        // Singleton, im Gegensatz zu den Stores darüber: Die Flächentabelle ist prozessweit und
        // überlebt die Anfrage, die sie geladen hat — das ist ihr ganzer Zweck. Sie holt sich den
        // DbContext über einen eigenen Scope, statt einen zu halten.
        services.AddSingleton<ISurfaceRouteTable, CachedSurfaceRouteTable>();
        services.AddScoped<IWorkspaceManagementStore, EfWorkspaceManagementStore>();
        services.AddScoped<IWorkspaceSurfaceStore, EfWorkspaceSurfaceStore>();
        // Der verengte Vertrag für Plugins: lesen, anlegen, löschen — ohne die
        // Identity-Provider-Zuweisung, die der volle Store mitträgt.
        // Singleton, nicht scoped: Ein Plugin löst seine Dienste einmal beim Start aus dem
        // Root-Provider auf, und von dort lässt sich nichts Scoped auflösen. Der Editor
        // öffnet den Scope pro Aufruf selbst.
        services.AddSingleton<Callora.Core.Application.Workspaces.Contracts.ISurfaceTreeEditor,
            Callora.Core.Application.Workspaces.WorkspaceSurfaceTreeEditor>();
        services.AddScoped<Callora.Core.Application.Surfaces.ISurfaceSessionStore, EfSurfaceSessionStore>();
        services.AddScoped<Callora.Core.Application.Surfaces.ISurfaceHandoffTicketStore, EfSurfaceHandoffTicketStore>();
        // Registered as the concrete type only: plugins reach resume tickets from outside any request
        // scope, so the contract itself is served by the singleton facade in host composition.
        services.AddScoped<EfSessionResumeTicketStore>();
        services.AddScoped<WorkspaceSurfaceProvisioner>();
        services.AddSingleton<
            Callora.Core.Application.Workspaces.Contracts.IWorkspaceSurfaceProvisioner,
            ScopedWorkspaceSurfaceProvisioner>();
        services.AddScoped<IWorkspaceTemplateRegistryStore, EfWorkspaceTemplateRegistryStore>();
        services.AddScoped<IWorkspaceThemeSettingsStore, EfWorkspaceThemeSettingsStore>();
        services.AddScoped<IWorkspaceSectionLayoutStore, EfWorkspaceSectionLayoutStore>();
        services.AddScoped<IPasswordHasher<BackendUser>, PasswordHasher<BackendUser>>();

        services.AddScoped<IHostAuditStore, DatabaseHostAuditStore>();
        services.AddScoped<BackendRbacDatabaseSeeder>();
        services.AddHostedService<HostDatabaseInitializationHostedService>();

        return services;
    }
}
