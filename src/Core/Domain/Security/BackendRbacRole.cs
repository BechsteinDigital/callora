namespace Callora.Core.Domain.Security;

public sealed class BackendRbacRole
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsSystem { get; set; }

    /// <summary>
    /// Das Plugin, dessen Installation diese Rolle angelegt hat, oder <see langword="null"/> für eine,
    /// die ein Mensch erstellt hat.
    /// </summary>
    /// <remarks>
    /// Zusammen mit <see cref="ProvisionedAs"/> die Identität einer bereitgestellten Rolle — und der
    /// Grund, warum sie nicht am Namen hängt: Wer die Rolle in der Oberfläche umbenennt, soll beim
    /// nächsten Start nicht eine zweite danebengestellt bekommen.
    /// </remarks>
    public string? ProvisionedByPluginId { get; set; }

    /// <summary>
    /// Welche Rolle des Plugins das ist (<c>admin</c> für die automatische Vollzugriffsrolle), oder
    /// <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Eigene Spalte statt eines zusammengesetzten Schlüssels, weil beide Teile einzeln gefragt werden:
    /// „welche Rollen gehören zu diesem Plugin" ist die Frage, die eine Deinstallation und jede spätere
    /// Übersicht stellt, und ein Präfix-Vergleich auf einer Sammelspalte ist genau die Art Abfrage, die
    /// später niemand mehr richtig hinbekommt.
    /// </remarks>
    public string? ProvisionedAs { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ICollection<BackendRbacRoleGrant> Permissions { get; set; } = new List<BackendRbacRoleGrant>();

    public ICollection<BackendRbacUserRole> UserAssignments { get; set; } = new List<BackendRbacUserRole>();
}
