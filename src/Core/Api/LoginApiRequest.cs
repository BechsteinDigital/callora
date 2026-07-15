namespace Callora.Core.Api;

public sealed record LoginApiRequest(
    string Login,
    string Password);
