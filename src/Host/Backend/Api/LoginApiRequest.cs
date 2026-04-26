namespace Callora.Host.Backend.Api;

public sealed record LoginApiRequest(
    string Login,
    string Password);
