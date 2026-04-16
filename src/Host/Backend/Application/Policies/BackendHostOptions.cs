namespace Callora.Host.Backend.Application.Policies;

public sealed class BackendHostOptions
{
    public string AuditFilePath { get; set; } =
        Path.Combine(AppContext.BaseDirectory, "plugins", "audit-log.jsonl");

    public string DatabasePath { get; set; } =
        Path.Combine(AppContext.BaseDirectory, "plugins", "host.db");

    public bool RequireAllowlistForActivation { get; set; }

    public string[] ActivationAllowlistPluginIds { get; set; } = [];

    public bool RequireApiKeyAuthentication { get; set; } = true;

    public string ApiKeyHeaderName { get; set; } = "X-Callora-Api-Key";

    public string[] ApiKeys { get; set; } = [];
}
