using Callora.Plugin.Communication.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Communication.Calls;

/// <summary>
/// The one rule for deciding whether two written numbers are the same line. It lived in
/// videoconference, which meant a quota keyed by a number and an assignment keyed by the same number
/// could disagree about what "the same" means.
/// </summary>
public sealed class PhoneNumberFormatTests
{
    [Theory]
    [InlineData("+493012345678")]
    [InlineData("00493012345678")]
    [InlineData("+49 (30) 1234-5678")]
    [InlineData("49 30 1234 5678")]
    public void PunctuationAndTheTwoWaysOfWritingACountryCode_AreTheSameLine(string written)
    {
        // An operator types the number the way their provider prints it; the trunk delivers it the way
        // the network happens to. Neither should have to guess the other's punctuation.
        Assert.Equal("493012345678", PhoneNumberFormat.Normalize(written));
    }

    [Fact]
    public void NationalAndInternationalForm_AreNotReconciled()
    {
        // The same line to a human, but only a country code would prove it — and guessing one would
        // silently claim somebody else's calls.
        Assert.NotEqual(
            PhoneNumberFormat.Normalize("+493012345678"),
            PhoneNumberFormat.Normalize("03012345678"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-")]
    [InlineData("n/a")]
    public void SomethingWithNoDigits_NormalizesToNothing(string? written)
    {
        // A caller must treat the empty result as "no number", never as a wildcard.
        Assert.Equal(string.Empty, PhoneNumberFormat.Normalize(written));
    }

    [Theory]
    [InlineData("+49 30 1234", true)]
    [InlineData("004930", true)]
    [InlineData("(030) 12-34", true)]
    [InlineData("crm", false)]
    [InlineData("dialer:campaign-x", false)]
    [InlineData("line-2", false)]
    [InlineData("", false)]
    public void WhatCountsAsANumberAtAll(string written, bool expected)
    {
        // Quota origins are not all numbers: "crm" and "dialer:campaign-x" are names a plugin passes,
        // and reducing them to digits would leave nothing at all.
        Assert.Equal(expected, PhoneNumberFormat.IsPhoneNumber(written));
    }

    [Fact]
    public void ANumberThatIsOnlyAnInternationalPrefix_StaysAsItIs()
    {
        // "00" alone would normalize to nothing; keeping the digits is the honest answer for input
        // that means nothing either way.
        Assert.Equal("00", PhoneNumberFormat.Normalize("00"));
    }
}
