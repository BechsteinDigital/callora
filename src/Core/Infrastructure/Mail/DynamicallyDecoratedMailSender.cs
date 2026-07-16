using Callora.Core.Application.Mail.Contracts;
using Callora.Core.Application.Plugins;

namespace Callora.Core.Infrastructure.Mail;

/// <summary>
/// Stable proxy that composes the plugin decorator chain for <see cref="IMailSender"/>
/// on every call from the live plugin catalog (REV2 §9.2), instead of freezing it at
/// first resolve (the static §9.1 anti-pattern). A decorator exported by a plugin
/// activated after the container was built takes effect on the next call, and a
/// deactivated plugin's decorator is dropped — so it no longer pins the plugin's
/// <c>AssemblyLoadContext</c>. This is the template for any future decoratable host
/// service: register the concrete base plus this kind of per-call proxy, never a
/// factory that captures the chain into a singleton.
/// </summary>
internal sealed class DynamicallyDecoratedMailSender : IMailSender
{
    private readonly IMailSender _baseSender;
    private readonly ICalloraPluginCatalog _pluginCatalog;

    public DynamicallyDecoratedMailSender(IMailSender baseSender, ICalloraPluginCatalog pluginCatalog)
    {
        _baseSender = baseSender;
        _pluginCatalog = pluginCatalog;
    }

    public Task SendAsync(MailMessage message, CancellationToken cancellationToken = default)
        => PluginServiceDecoration.Decorate(_baseSender, _pluginCatalog)
            .SendAsync(message, cancellationToken);
}
