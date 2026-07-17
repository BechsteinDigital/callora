namespace Callora.Core.Application.Security;

/// <summary>
/// A backend user operation was rejected with a caller-facing validation fault, e.g. a
/// missing password when creating a user.
/// </summary>
public sealed class BackendUserException : CalloraException
{
    private const int BadRequest = 400;

    private BackendUserException(string errorCode, int statusCode, string message)
        : base(errorCode, statusCode, message)
    {
    }

    /// <summary>Error code for a create request that omits the required password.</summary>
    public const string PasswordRequiredCode = "USER__PASSWORD_REQUIRED";

    /// <summary>A new user was created without the required password.</summary>
    public static BackendUserException PasswordRequired() =>
        new(PasswordRequiredCode, BadRequest, "Password is required when creating a new user.");
}
