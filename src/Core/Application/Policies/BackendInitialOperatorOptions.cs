namespace Callora.Core.Application.Policies;

/// <summary>
/// One-time bootstrap operator, seeded on first start ONLY when the user table
/// is empty — e.g. a fresh production deployment where the demo admin is disabled
/// and there is otherwise no way to sign in. Unlike
/// <see cref="BackendDemoAdminUserOptions"/> this seeds once and never overwrites
/// an existing install, so a password changed later through the admin UI survives
/// restarts. Credentials come from configuration / <c>.env</c>; disabled by default.
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
