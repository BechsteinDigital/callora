using Callora.Core.Application.Security;
using Callora.Core.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Callora.Core.Infrastructure.Persistence;

/// <summary>
/// Legt für jedes installierte Plugin die Rolle an, die es nach sich zieht.
/// </summary>
/// <remarks>
/// <para>
/// <b>Einmal anlegen, danach nie wieder anfassen.</b> Der Betreiber soll die Rolle zuschneiden dürfen —
/// Schlüssel herausnehmen, andere ergänzen, sie umbenennen — und das muss einen Neustart überleben. Eine
/// Anpassung, die beim nächsten Start still zurückgedreht wird, ist schlimmer als eine fehlende
/// Berechtigung: Die fehlende sieht man, die zurückgedrehte nicht.
/// </para>
/// <para>
/// Was ein Plugin-Update an neuen Schlüsseln mitbringt, landet deshalb nicht in der Rolle, sondern in
/// einer Logzeile. Das ist bewusst die schwächere Zustellung: Sie kann übersehen werden, aber sie kann
/// keine Entscheidung überschreiben, die jemand getroffen hat.
/// </para>
/// <para>
/// <b>Die Identität ist das Paar aus Plugin und Slug, nicht der Name.</b> Ein umbenanntes
/// <c>pbx.admin</c> wird wiedergefunden; hinge die Suche am Namen, stünde beim nächsten Start eine
/// zweite Rolle daneben und niemand wüsste, welche die echte ist.
/// </para>
/// </remarks>
public sealed class PluginRoleProvisioner(
    IPluginRoleTemplateSource templates,
    ILogger<PluginRoleProvisioner> logger)
{
    private readonly IPluginRoleTemplateSource _templates =
        templates ?? throw new ArgumentNullException(nameof(templates));

    /// <summary>
    /// Legt fehlende Rollen an und antwortet, wie viele es waren.
    /// </summary>
    public async Task<int> ProvisionAsync(
        HostPersistenceDbContext dbContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var wanted = await _templates.ListAsync(cancellationToken).ConfigureAwait(false);
        if (wanted.Count == 0)
        {
            return 0;
        }

        var created = 0;
        foreach (var template in wanted)
        {
            if (await ProvisionOneAsync(dbContext, template, cancellationToken).ConfigureAwait(false))
            {
                created++;
            }
        }

        if (created > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return created;
    }

    private async Task<bool> ProvisionOneAsync(
        HostPersistenceDbContext dbContext,
        PluginRoleTemplate template,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.BackendRbacRoles
            .Include(role => role.Permissions)
            .SingleOrDefaultAsync(
                role => role.ProvisionedByPluginId == template.PluginId
                    && role.ProvisionedAs == template.Slug,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            ReportDrift(existing, template);
            return false;
        }

        // Der Rollenname ist global eindeutig. Eine gleichnamige Rolle, die ein Mensch angelegt hat,
        // wird nicht übernommen: Sie gehört ihm, ihre Berechtigungen sind seine Entscheidung, und sie
        // unter das Plugin zu hängen hieße, sie beim Deinstallieren mitzunehmen. Gemeldet statt
        // erzwungen — ein Startabbruch wegen eines Namens wäre die schlechtere Antwort.
        var nameTaken = await dbContext.BackendRbacRoles
            .AnyAsync(role => role.Name == template.RoleName, cancellationToken)
            .ConfigureAwait(false);

        if (nameTaken)
        {
            logger.LogWarning(
                "Die Rolle '{RoleName}' für Plugin {PluginId} wurde nicht angelegt: Der Name ist bereits "
                + "vergeben. Die Berechtigungen des Plugins müssen von Hand vergeben werden.",
                template.RoleName, template.PluginId);
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        dbContext.BackendRbacRoles.Add(new BackendRbacRole
        {
            Id = Guid.NewGuid(),
            Name = template.RoleName,
            // Kein Systemrolle: Der Betreiber soll sie ändern dürfen. IsSystem sperrt den Schreibpfad
            // (BackendRbacException.RoleFixed), und eine unveränderliche Rolle wäre genau das Gegenteil
            // dessen, wofür sie da ist.
            IsSystem = false,
            ProvisionedByPluginId = template.PluginId,
            ProvisionedAs = template.Slug,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Permissions = [.. template.PermissionKeys.Select(key => new BackendRbacRoleGrant
            {
                Id = Guid.NewGuid(),
                PermissionKey = key
            })]
        });

        logger.LogInformation(
            "Rolle '{RoleName}' für Plugin {PluginId} angelegt, mit {Count} Berechtigung(en).",
            template.RoleName, template.PluginId, template.PermissionKeys.Count);

        return true;
    }

    private void ReportDrift(BackendRbacRole existing, PluginRoleTemplate template)
    {
        var granted = existing.Permissions
            .Select(grant => grant.PermissionKey)
            .ToHashSet(StringComparer.Ordinal);

        var missing = template.PermissionKeys.Where(key => !granted.Contains(key)).ToArray();
        if (missing.Length == 0)
        {
            return;
        }

        // Sowohl „das Plugin hat dazugelernt" als auch „der Betreiber hat bewusst etwas herausgenommen"
        // sehen von hier aus gleich aus, und es gibt keinen Weg, sie zu unterscheiden. Deshalb wird
        // gemeldet und nicht entschieden.
        logger.LogInformation(
            "Rolle '{RoleName}' hat {Count} von Plugin {PluginId} deklarierte Berechtigung(en) nicht: "
            + "{Missing}. Sie wird nicht angefasst — nachtragen, falls gewollt.",
            existing.Name, missing.Length, template.PluginId, string.Join(", ", missing));
    }
}
