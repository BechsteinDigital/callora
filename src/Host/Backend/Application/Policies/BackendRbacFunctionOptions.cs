namespace Callora.Host.Backend.Application.Policies;

public sealed class BackendRbacFunctionOptions
{
    public string Function { get; set; } = string.Empty;

    public string[] Actions { get; set; } = [];
}
