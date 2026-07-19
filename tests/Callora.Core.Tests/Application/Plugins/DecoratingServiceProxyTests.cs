using Callora.Core.Application.Extensibility.Contracts;
using Callora.Core.Application.Mail.Contracts;
using Callora.Core.Application.Plugins;
using Callora.Core.Tests.Support;
using Xunit;

namespace Callora.Core.Tests.Application.Plugins;

public sealed class DecoratingServiceProxyTests
{
    [Fact]
    public async Task Recomposes_the_chain_per_call_so_activation_and_deactivation_take_effect()
    {
        var baseSender = new RecordingMailSender();
        // StaticPluginCatalog reads this dictionary live on every GetExports, so mutating
        // it models a plugin activating/deactivating after the proxy exists.
        var exports = new Dictionary<Type, IReadOnlyList<object>>();
        var catalog = new StaticPluginCatalog(exports);
        var sut = DecoratingServiceProxy<IMailSender>.Wrap(baseSender, catalog);

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

    [Fact]
    public void Forwards_calls_and_return_values_for_any_interface()
    {
        var catalog = new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>());
        var greeter = DecoratingServiceProxy<IGreeter>.Wrap(new BaseGreeter(), catalog);

        Assert.Equal("Hi, Ada", greeter.Greet("Ada"));
        Assert.Equal(42, greeter.Answer());
    }

    [Fact]
    public void Applies_an_exported_decorator_for_any_interface()
    {
        var exports = new Dictionary<Type, IReadOnlyList<object>>
        {
            [typeof(IServiceDecorator<IGreeter>)] = new object[] { new ShoutingGreeterDecorator() },
        };
        var greeter = DecoratingServiceProxy<IGreeter>.Wrap(new BaseGreeter(), new StaticPluginCatalog(exports));

        Assert.Equal("HI, ADA", greeter.Greet("Ada"));
    }

    [Fact]
    public void Unwraps_the_services_own_exception_not_the_reflection_wrapper()
    {
        var catalog = new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>());
        var greeter = DecoratingServiceProxy<IGreeter>.Wrap(new BaseGreeter(), catalog);

        // The proxy forwards via reflection; a caller must still see the service's real
        // exception, not the TargetInvocationException the reflection call would wrap it in.
        var exception = Assert.Throws<InvalidOperationException>(() => greeter.Boom());
        Assert.Equal("boom", exception.Message);
    }

    [Fact]
    public async Task Surfaces_the_real_exception_from_a_faulted_async_method()
    {
        var catalog = new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>());
        var greeter = DecoratingServiceProxy<IGreeter>.Wrap(new BaseGreeter(), catalog);

        // An async method returns a faulted Task rather than throwing synchronously — the
        // proxy forwards the Task, so the real exception surfaces on await, not a wrapper.
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => greeter.BoomAsync());
        Assert.Equal("async boom", exception.Message);
    }

    public interface IGreeter
    {
        string Greet(string name);

        int Answer();

        void Boom();

        Task BoomAsync();
    }

    private sealed class BaseGreeter : IGreeter
    {
        public string Greet(string name) => $"Hi, {name}";

        public int Answer() => 42;

        public void Boom() => throw new InvalidOperationException("boom");

        public Task BoomAsync() => Task.FromException(new InvalidOperationException("async boom"));
    }

    private sealed class ShoutingGreeterDecorator : IServiceDecorator<IGreeter>
    {
        public int Order => 1;

        public IGreeter Decorate(IGreeter inner) => new Shouter(inner);

        private sealed class Shouter(IGreeter inner) : IGreeter
        {
            public string Greet(string name) => inner.Greet(name).ToUpperInvariant();

            public int Answer() => inner.Answer();

            public void Boom() => inner.Boom();

            public Task BoomAsync() => inner.BoomAsync();
        }
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
