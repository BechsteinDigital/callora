namespace Callora.Administration.Api;

public sealed record RbacFunctionActionApiRequest(
    string Function,
    IReadOnlyList<string> Actions);
