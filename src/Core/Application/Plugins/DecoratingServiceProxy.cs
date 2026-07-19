using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// Per-call decorating proxy for any decoratable host-service interface. On every call
/// it composes the plugin decorator chain from the live plugin catalog (REV2 §9.2) and
/// forwards to it: a decorator exported by a plugin activated after the container was
/// built takes effect on the next call, and a deactivated plugin's decorator is dropped
/// so it no longer pins the plugin's <c>AssemblyLoadContext</c>. Generalizes the former
/// hand-written per-service proxy — register a decoratable interface with
/// <c>AddDecoratableSingleton</c> instead of writing one proxy type per service.
/// </summary>
/// <typeparam name="TService">The decoratable service interface.</typeparam>
// Not sealed and instantiated through DispatchProxy.Create, which emits a runtime
// subclass; CA1852 (seal internal types) therefore does not apply.
#pragma warning disable CA1852
internal class DecoratingServiceProxy<TService> : DispatchProxy
    where TService : class
{
    private TService _baseService = null!;
    private ICalloraPluginCatalog _pluginCatalog = null!;

    /// <summary>Wraps <paramref name="baseService"/> in a per-call decorating proxy.</summary>
    public static TService Wrap(TService baseService, ICalloraPluginCatalog pluginCatalog)
    {
        ArgumentNullException.ThrowIfNull(baseService);
        ArgumentNullException.ThrowIfNull(pluginCatalog);

        var proxy = Create<TService, DecoratingServiceProxy<TService>>();
        var self = (DecoratingServiceProxy<TService>)(object)proxy!;
        self._baseService = baseService;
        self._pluginCatalog = pluginCatalog;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        var effective = PluginServiceDecoration.Decorate(_baseService, _pluginCatalog);
        try
        {
            return targetMethod.Invoke(effective, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            // Surface the service's real exception (and its stack), not the reflection wrapper.
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw; // unreachable — Throw() above always throws.
        }
    }
}
#pragma warning restore CA1852
