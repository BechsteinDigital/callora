namespace Callora.Core.Application.Policies;

/// <summary>
/// The operator account, seeded on first start ONLY when the user table is empty —
/// a fresh deployment that would otherwise have no way to sign in. It seeds once and
/// never overwrites an existing install, so a password changed later through the admin
/// UI survives restarts.
/// <para>
/// There used to be a second, re-seeding demo account beside this one. It set its
/// credentials on every start, which quietly undid any password an operator had
/// changed — and the secret-hygiene check stayed silent about it as soon as a custom
/// password was configured. This is now the only way in.
/// </para> Credentials come from configuration / <c>.env</c>; disabled by default.
/// The bootstrap password must meet a minimum length (a too-short password is
/// refused, not weakened). While enabled, a warning is logged on every start
/// reminding operators to rotate the password and disable this after first sign-in.
/// </summary>
public sealed class BackendInitialOperatorOptions
{
    public bool Enabled { get; set; }

    public string ExternalId { get; set; } = "admin";

    public string? Email { get; set; }

    public string? DisplayName { get; set; }

    public string Password { get; set; } = string.Empty;
}
