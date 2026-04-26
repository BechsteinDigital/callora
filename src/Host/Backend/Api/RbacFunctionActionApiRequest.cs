namespace Callora.Host.Backend.Api;

public sealed record RbacFunctionActionApiRequest(
    string Function,
    IReadOnlyList<string> Actions);
