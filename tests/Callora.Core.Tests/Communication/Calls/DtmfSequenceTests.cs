using Callora.Plugin.Communication.Application.Calls;
using Xunit;

namespace Callora.Core.Tests.Communication.Calls;

/// <summary>
/// A keypad has sixteen tones and nothing else. Validating the whole sequence up front is what makes
/// a rejected request leave the call untouched instead of half-dialled (#116).
/// </summary>
public sealed class DtmfSequenceTests
{
    [Theory]
    [InlineData("0123456789")]
    [InlineData("*#")]
    [InlineData("ABCD")]
    public void EveryKeypadToneIsAccepted(string tones) =>
        Assert.Equal(tones.ToCharArray(), DtmfSequence.Parse(tones));

    [Fact]
    public void LowercaseHexToneMeansTheSameKey() =>
        Assert.Equal(['A', 'B'], DtmfSequence.Parse("ab"));

    [Fact]
    public void SurroundingWhitespaceIsIgnored() =>
        Assert.Equal(['1', '2'], DtmfSequence.Parse("  12 "));

    [Theory]
    [InlineData("12X4")]
    [InlineData("1 2")]
    [InlineData("E")]
    [InlineData("+4930")]
    public void AnythingElseRejectsTheWholeSequence(string tones) =>
        Assert.Throws<ArgumentException>(() => DtmfSequence.Parse(tones));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptySequenceIsRejected(string? tones) =>
        Assert.ThrowsAny<ArgumentException>(() => DtmfSequence.Parse(tones!));

    [Fact]
    public void AnOverLongSequenceIsRejected()
    {
        // An extension or a menu path, not a payload.
        var tones = new string('1', DtmfSequence.MaxLength + 1);

        Assert.Throws<ArgumentException>(() => DtmfSequence.Parse(tones));
    }

    [Fact]
    public void TheLongestAllowedSequenceIsAccepted() =>
        Assert.Equal(DtmfSequence.MaxLength, DtmfSequence.Parse(new string('1', DtmfSequence.MaxLength)).Count);
}
