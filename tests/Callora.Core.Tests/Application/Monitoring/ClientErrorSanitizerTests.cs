using Callora.Core.Application.Monitoring;

namespace Callora.Core.Tests.Application.Monitoring;

/// <summary>
/// Die Senke aus #294 nimmt Meldungen aus fremden Browsern entgegen — auf der öffentlichen Fläche
/// von jedem. Was hier hindurchgeht, landet im Betriebslog und von dort über OpenTelemetry beim
/// Ziel. Der Inhalt wird deshalb nicht ausgewertet, aber er wird entschärft.
/// </summary>
public sealed class ClientErrorSanitizerTests
{
    // Ein Stacktrace aus dem Browser eines Besuchers trägt die URLs mit, an denen er entstanden
    // ist — samt allem, was in ihrer Query steht. Genau davor warnt das Issue.
    [Fact]
    public void Sanitize_UrlWithQueryAndFragment_KeepsOnlyThePath()
    {
        var sanitized = ClientErrorSanitizer.Sanitize(new ClientErrorReport(
            Source: "surface",
            Message: "boom",
            Stack: null,
            Url: "https://kunde.example/portal/termin?email=anna%40example.org&token=abc#step2"));

        Assert.Equal("https://kunde.example/portal/termin", sanitized.Url);
    }

    [Fact]
    public void Sanitize_QueryInsideFreeText_IsCutAtTheQuestionMark()
    {
        var sanitized = ClientErrorSanitizer.Sanitize(Report(
            message: "Failed to fetch https://kunde.example/api/kunden?email=anna%40example.org",
            stack: "at load (https://kunde.example/assets/app.js?token=geheim:12:3)"));

        Assert.Equal("Failed to fetch https://kunde.example/api/kunden?…", sanitized.Message);
        Assert.Equal("at load (https://kunde.example/assets/app.js?…:12:3)", sanitized.Stack);
    }

    // Ein Fragezeichen im Fließtext ist kein Query-String. Wer das nicht unterscheidet, verstümmelt
    // die Meldung, die er eigentlich lesbar halten will.
    [Fact]
    public void Sanitize_QuestionMarkInProse_IsLeftAlone()
    {
        var sanitized = ClientErrorSanitizer.Sanitize(Report(message: "Wirklich löschen? Abbruch."));

        Assert.Equal("Wirklich löschen? Abbruch.", sanitized.Message);
    }

    // Ohne das schreibt ein Absender sich eigene Logzeilen: ein "\n" im Text, und was danach kommt,
    // liest sich wie ein Eintrag des Systems.
    [Fact]
    public void Sanitize_ControlCharacters_AreReplacedSoNobodyWritesTheirOwnLogLines()
    {
        var sanitized = ClientErrorSanitizer.Sanitize(Report(
            message: "boom\n2026-08-16 12:00:00 WARN  Datenbank gelöscht\r\n"));

        Assert.Equal("boom 2026-08-16 12:00:00 WARN  Datenbank gelöscht", sanitized.Message);
    }

    [Fact]
    public void Sanitize_LongStack_IsTruncatedToItsLimit()
    {
        var sanitized = ClientErrorSanitizer.Sanitize(Report(stack: new string('x', 9_000)));

        Assert.Equal(ClientErrorSanitizer.MaxStackLength, sanitized.Stack!.Length);
        Assert.EndsWith("…", sanitized.Stack, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitize_UnknownSource_FallsBackInsteadOfEchoingWhatWasSent()
    {
        // Die Herkunft benennt der Absender, und sie steht später in einem Logfeld, nach dem
        // jemand filtert. Ein freier String wäre genau die Auswertung, die hier nicht stattfindet.
        var sanitized = ClientErrorSanitizer.Sanitize(Report(source: "<script>irgendwas</script>"));

        Assert.Equal("unknown", sanitized.Source);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("surface")]
    public void Sanitize_KnownSource_IsKept(string source)
    {
        Assert.Equal(source, ClientErrorSanitizer.Sanitize(Report(source: source)).Source);
    }

    [Fact]
    public void Sanitize_RelativeUrl_KeepsThePathAndDropsTheQuery()
    {
        var sanitized = ClientErrorSanitizer.Sanitize(Report(url: "/portal/termin?token=abc"));

        Assert.Equal("/portal/termin", sanitized.Url);
    }

    [Fact]
    public void Sanitize_EmptyMessage_StaysEmptyRatherThanBecomingNull()
    {
        Assert.Equal(string.Empty, ClientErrorSanitizer.Sanitize(Report(message: "  ")).Message);
    }

    private static ClientErrorReport Report(
        string source = "surface",
        string message = "boom",
        string? stack = null,
        string? url = null)
        => new(source, message, stack, url);
}
