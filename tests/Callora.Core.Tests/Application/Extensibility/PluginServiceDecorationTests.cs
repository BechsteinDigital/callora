using Callora.Core.Tests.Support;
using Callora.Core.Application.Extensibility.Contracts;
using Callora.Core.Application.Plugins;

namespace Callora.Core.Tests.Application.Extensibility;

public sealed class PluginServiceDecorationTests
{
    public interface IGreeter
    {
        string Greet();
    }

    [Fact]
    public void Decorate_WithNoDecorators_ReturnsBaseService()
    {
        var baseService = new BaseGreeter();
        var catalog = new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>());

        var result = PluginServiceDecoration.Decorate<IGreeter>(baseService, catalog);

        Assert.Same(baseService, result);
        Assert.Equal("hello", result.Greet());
    }

    [Fact]
    public void Decorate_AppliesDecoratorsByOrder_LowestClosestToBase()
    {
        var baseService = new BaseGreeter();
        var catalog = new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>
        {
            [typeof(IServiceDecorator<IGreeter>)] =
            [
                new BracketDecorator("outer", order: 10),
                new BracketDecorator("inner", order: 1)
            ]
        });

        var result = PluginServiceDecoration.Decorate<IGreeter>(baseService, catalog);

        // inner (order 1) wraps the base first, outer (order 10) wraps that.
        Assert.Equal("outer(inner(hello))", result.Greet());
    }

    private sealed class BaseGreeter : IGreeter
    {
        public string Greet() => "hello";
    }

    private sealed class BracketDecorator(string label, int order) : IServiceDecorator<IGreeter>
    {
        public int Order => order;
        public IGreeter Decorate(IGreeter inner) => new Wrapped(label, inner);

        private sealed class Wrapped(string label, IGreeter inner) : IGreeter
        {
            public string Greet() => $"{label}({inner.Greet()})";
        }
    }
}
