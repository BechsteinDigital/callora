namespace Callora.Core.Application.Policies;

public sealed class BackendRbacUserAssignmentOptions
{
    public string UserId { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
}
