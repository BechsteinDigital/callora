namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// The one rule for deciding whether two written numbers are the same line.
/// </summary>
/// <remarks>
/// <para>Punctuation and the two ways of writing a country code are ignored: an operator types the
/// number the way their provider prints it, and the trunk delivers it the way the network happens to.
/// Neither side should have to guess the other's punctuation.</para>
/// <para>National and international form are deliberately <b>not</b> reconciled. <c>030 1234</c> and
/// <c>+49 30 1234</c> are the same line to a human, but only a country code would prove it, and
/// guessing one would silently claim somebody else's calls.</para>
/// <para>It lives in the contract because more than one thing keys on a number — a line's quota, a
/// consumer's assignment — and two rules that nearly agree are worse than one that is strict.</para>
/// </remarks>
public static class PhoneNumberFormat
{
    /// <summary>
    /// Reduces a number to the digits it is matched by. Returns an empty string when there is nothing
    /// to match on — a caller must treat that as "no number", never as a wildcard.
    /// </summary>
    public static string Normalize(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return string.Empty;
        }

        var digits = new string([.. number.Where(char.IsAsciiDigit)]);

        // "00" is the international prefix written out; "+" is the same thing and has already been
        // dropped with the punctuation. A number that is nothing but the prefix keeps its digits:
        // the input means nothing either way, and returning empty would call it "no number".
        return digits.Length > 2 && digits.StartsWith("00", StringComparison.Ordinal)
            ? digits[2..]
            : digits;
    }

    /// <summary>
    /// Whether a string is a telephone number rather than a name.
    /// </summary>
    /// <remarks>
    /// Quota origins are not all numbers: <c>crm</c> and <c>dialer:campaign-x</c> are names a plugin
    /// passes, and reducing them to digits would leave nothing at all. A number carries at least one
    /// digit and nothing but the punctuation numbers are written with.
    /// </remarks>
    public static bool IsPhoneNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var sawDigit = false;
        foreach (var character in value)
        {
            if (char.IsAsciiDigit(character))
            {
                sawDigit = true;
                continue;
            }

            if (character is not ('+' or '-' or '(' or ')' or '.' or '/' or ' '))
            {
                return false;
            }
        }

        return sawDigit;
    }
}
