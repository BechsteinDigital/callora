using Callora.Core.Application.Extensibility.Contracts;
using Callora.Core.Application.Mail.Contracts;
using Callora.Core.Infrastructure.Mail;
using Callora.Core.Tests.Support;

namespace Callora.Core.Tests.Infrastructure.Mail;

public sealed class DynamicallyDecoratedMailSenderTests
{
    [Fact]
    public async Task Recomposes_the_chain_per_call_so_activation_and_deactivation_take_effect()
    {
        var baseSender = new RecordingMailSender();
        // StaticPluginCatalog reads this dictionary live on every GetExports, so
        // mutating it models a plugin activating/deactivating after the proxy exists.
        var exports = new Dictionary<Type, IReadOnlyList<object>>();
        var catalog = new StaticPluginCatalog(exports);
        var sut = new DynamicallyDecoratedMailSender(baseSender, catalog);

        // 1) No decorator exported yet → base receives the message unchanged.
        await sut.SendAsync(new MailMessage("a@x", "one", "body"));

        // A plugin activates and exports a decorator AFTER the proxy was built.
        exports[typeof(IServiceDecorator<IMailSender>)] = new object[] { new SubjectTagDecorator() };

        // 2) The freshly exported decorator takes effect — dynamic, not frozen (REV2 §9.2).
        await sut.SendAsync(new MailMessage("b@x", "two", "body"));

        // The plugin deactivates; its export is removed from the live catalog.
        exports.Remove(typeof(IServiceDecorator<IMailSender>));

        // 3) The decorator is no longer applied — the deactivated plugin is not pinned.
        await sut.SendAsync(new MailMessage("c@x", "three", "body"));

        Assert.Equal("one", baseSender.Sent[0].Subject);
        Assert.Equal("[tagged] two", baseSender.Sent[1].Subject);
        Assert.Equal("three", baseSender.Sent[2].Subject);
    }

    private sealed class SubjectTagDecorator : IServiceDecorator<IMailSender>
    {
        public int Order => 1;

        public IMailSender Decorate(IMailSender inner) => new Tagger(inner);

        private sealed class Tagger(IMailSender inner) : IMailSender
        {
            public Task SendAsync(MailMessage message, CancellationToken cancellationToken = default)
                => inner.SendAsync(message with { Subject = "[tagged] " + message.Subject }, cancellationToken);
        }
    }
}
