namespace Callora.Host.Backend.Application.Policies;

public sealed class BackendDemoAdminUserOptions
{
    public bool Enabled { get; set; } = true;

    public string ExternalId { get; set; } = "admin";

    public string Email { get; set; } = "admin@callora.local";

    public string DisplayName { get; set; } = "Callora Admin";

    public string Password { get; set; } = "admin123!";
}
