using Callora.Core.Application.Mail.Contracts;
using Callora.Core.Extensibility;
using Xunit;

namespace Callora.Core.Tests.Extensibility;

public sealed class CalloraExtensibleAttributeTests
{
    [Fact]
    public void Parameterless_DefaultsToContributable()
    {
        var attribute = new CalloraExtensibleAttribute();

        Assert.Equal(ExtensionPointMode.Contributable, attribute.Mode);
        Assert.Null(attribute.Note);
    }

    [Fact]
    public void NoteOnly_DefaultsToContributable_AndKeepsNote()
    {
        var attribute = new CalloraExtensibleAttribute("guidance");

        Assert.Equal(ExtensionPointMode.Contributable, attribute.Mode);
        Assert.Equal("guidance", attribute.Note);
    }

    [Fact]
    public void ModeConstructor_SetsModeWithoutNote()
    {
        var attribute = new CalloraExtensibleAttribute(ExtensionPointMode.Replaceable);

        Assert.Equal(ExtensionPointMode.Replaceable, attribute.Mode);
        Assert.Null(attribute.Note);
    }

    [Fact]
    public void ModeConstructor_SetsBothModeAndNote()
    {
        var attribute = new CalloraExtensibleAttribute(ExtensionPointMode.Decoratable, "wrap it");

        Assert.Equal(ExtensionPointMode.Decoratable, attribute.Mode);
        Assert.Equal("wrap it", attribute.Note);
    }

    [Fact]
    public void MailSender_IsClassifiedAsADecoratableExtensionPoint()
    {
        // The one host service already run through the decoration pipeline must be
        // classified so the extension surface is discoverable, not implicit.
        var attribute = (CalloraExtensibleAttribute?)Attribute.GetCustomAttribute(
            typeof(IMailSender), typeof(CalloraExtensibleAttribute));

        Assert.NotNull(attribute);
        Assert.Equal(ExtensionPointMode.Decoratable, attribute!.Mode);
    }
}
