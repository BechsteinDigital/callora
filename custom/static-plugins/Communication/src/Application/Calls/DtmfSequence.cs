namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// Validates a DTMF sequence before any of it is sent. A keypad has sixteen tones and nothing else;
/// a sequence with an invalid character is rejected whole rather than half-sent, so a caller never
/// has to guess how far a bad request got.
/// </summary>
public static class DtmfSequence
{
    /// <summary>Longest sequence accepted in one request — an extension or a menu path, not a payload.</summary>
    public const int MaxLength = 32;

    /// <summary>
    /// Parses the sequence, upper-casing the hexadecimal tones so <c>a</c> and <c>A</c> mean the same
    /// key. Throws <see cref="ArgumentException"/> for an empty, over-long or invalid sequence.
    /// </summary>
    public static IReadOnlyList<char> Parse(string tones)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tones);

        var trimmed = tones.Trim();
        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException(
                $"A DTMF sequence may carry at most {MaxLength} tones.", nameof(tones));
        }

        var parsed = new char[trimmed.Length];
        for (var index = 0; index < trimmed.Length; index++)
        {
            var tone = char.ToUpperInvariant(trimmed[index]);
            if (!IsTone(tone))
            {
                throw new ArgumentException(
                    $"'{trimmed[index]}' is not a DTMF tone; use 0-9, *, # or A-D.", nameof(tones));
            }

            parsed[index] = tone;
        }

        return parsed;
    }

    /// <summary>Whether the character is one of the sixteen DTMF tones.</summary>
    public static bool IsTone(char tone) =>
        tone is (>= '0' and <= '9') or '*' or '#' or (>= 'A' and <= 'D');
}
