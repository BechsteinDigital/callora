namespace Callora.Core.Api;

public sealed record RbacFunctionActionApiRequest(
    string Function,
    IReadOnlyList<string> Actions);
