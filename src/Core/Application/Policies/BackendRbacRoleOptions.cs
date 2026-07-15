namespace Callora.Core.Application.Policies;

public sealed class BackendRbacRoleOptions
{
    public string Role { get; set; } = string.Empty;

    public BackendRbacFunctionOptions[] Functions { get; set; } = [];
}
