namespace Callora.Core.Application.Security;

/// <summary>
/// The single password policy for every local credential (#104): the bootstrap
/// operator seed, operator-created accounts and later credential changes all pass
/// through here, so no path can set a weaker password than another.
/// </summary>
public static class BackendPasswordPolicy
{
    /// <summary>Minimum length for any local password.</summary>
    public const int MinimumLength = 12;

    /// <summary>Upper bound, so a hashing call can never be turned into a DoS.</summary>
    public const int MaximumLength = 256;

    /// <summary>
    /// Returns null when <paramref name="password"/> is acceptable, otherwise a
    /// message describing the violated rule. The message names the rule, never the
    /// supplied value.
    /// </summary>
    public static string? Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return "A password is required.";
        }

        if (password.Length < MinimumLength)
        {
            return $"The password must be at least {MinimumLength} characters long.";
        }

        if (password.Length > MaximumLength)
        {
            return $"The password must not exceed {MaximumLength} characters.";
        }

        return null;
    }

    /// <summary>Whether <paramref name="password"/> satisfies the policy.</summary>
    public static bool IsAcceptable(string? password) => Validate(password) is null;
}
