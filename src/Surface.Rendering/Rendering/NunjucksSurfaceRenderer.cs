using Jint;
using Jint.Runtime;
using System.Text.Json;

namespace Callora.Surface.Rendering.Rendering;

/// <summary>
/// The surface renderer (ADR-015 rev.): the bundled Nunjucks engine — which has
/// NATIVE Twig-style inheritance (<c>extends</c>/<c>block</c>/<c>super()</c>) —
/// run on the Jint JS interpreter in a hardened sandbox. Jint gets NO CLR access
/// (JS can never reach a .NET type); a wall-clock timeout, memory, recursion and
/// statement limits bound template DoS; the context is passed as JSON only. Each
/// render uses a FRESH engine, so templates cannot contaminate one another (§8).
/// </summary>
public sealed class NunjucksSurfaceRenderer : ISurfaceRenderer
{
    private const int TimeoutSeconds = 2;
    private const long MemoryLimitBytes = 32L * 1024 * 1024;
    private const int RecursionLimit = 64;
    private const int MaxStatements = 2_000_000;
    private const int MaxOutputChars = 512 * 1024;

    // The Nunjucks bundle source, loaded once and executed on each fresh engine.
    private static readonly string NunjucksSource = LoadNunjucksSource();

    // Templates address the context in JavaScript spelling ({{ view.displayName }}),
    // so records serialize camelCase and enums as their names rather than ordinals.
    // Dictionary keys (theme tokens, slot names) are left exactly as declared.
    private static readonly JsonSerializerOptions ContextSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    // Binds the UMD bundle's global lookups, installs the render harness, and (when
    // includes are enabled) a callback loader that defers file access to .NET.
    private const string RenderHarness =
        """
        var window = this; var self = this; var global = this;
        """;

    private const string RenderScript =
        """
        (function () {
            var options = { autoescape: true, throwOnUndefined: false };
            var env;
            if (typeof __loadTemplate === 'function') {
                function CalloraLoader() {}
                CalloraLoader.prototype.getSource = function (name) {
                    var src = __loadTemplate(name);
                    if (src === null || src === undefined) {
                        throw new Error('Template not found or not in scope: ' + name);
                    }
                    return { src: src, path: name, noCache: true };
                };
                env = new nunjucks.Environment(new CalloraLoader(), options);
            } else {
                env = new nunjucks.Environment(null, options);
            }
            var context = JSON.parse(__contextJson);
            installCalloraComposition(env, context);
            return env.renderString(__templateText, context);
        })();
        """;

    // Composition rides on Nunjucks' own inheritance rather than beside it: a theme
    // declares slots inside its blocks, so extends/block/super() keep working and a
    // child theme can wrap, move or replace a slot the way it does any other markup.
    // The globals only read what the host already resolved into the context.
    private const string CompositionScript =
        """
        function calloraAttr(value) {
            return String(value)
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;')
                .replace(/'/g, '&#39;');
        }

        function calloraProps(props) {
            if (props === undefined || props === null || typeof props !== 'object') {
                return null;
            }
            var copy = {};
            var empty = true;
            for (var key in props) {
                // Nunjucks appends this marker when a call uses keyword arguments.
                if (key === '__keywords' || !Object.prototype.hasOwnProperty.call(props, key)) {
                    continue;
                }
                copy[key] = props[key];
                empty = false;
            }
            return empty ? null : copy;
        }

        function calloraIsland(view, slot, props) {
            var markup = '<div class="callora-island"'
                + ' data-callora-island="' + calloraAttr(view.viewId) + '"'
                + ' data-callora-slot="' + calloraAttr(slot) + '"'
                + ' data-callora-plugin="' + calloraAttr(view.pluginId) + '"';
            var payload = calloraProps(props);
            if (payload !== null) {
                markup += ' data-callora-props="' + calloraAttr(JSON.stringify(payload)) + '"';
            }
            return markup + '></div>';
        }

        function installCalloraComposition(env, context) {
            var slots = (context && context.slots) || {};
            var safe = function (html) { return new nunjucks.runtime.SafeString(html); };

            env.addGlobal('callora_slot', function (name, props) {
                var views = slots[name] || [];
                var html = '';
                for (var i = 0; i < views.length; i++) {
                    html += calloraIsland(views[i], name, props);
                }
                return safe(html);
            });

            env.addGlobal('callora_view', function (viewId, props) {
                for (var name in slots) {
                    var views = slots[name];
                    for (var i = 0; i < views.length; i++) {
                        if (views[i].viewId === viewId) {
                            return safe(calloraIsland(views[i], name, props));
                        }
                    }
                }
                return safe('');
            });

            env.addGlobal('callora_slot_views', function (name) {
                return slots[name] || [];
            });

            env.addGlobal('callora_has_slot', function (name) {
                return (slots[name] || []).length > 0;
            });

            var navigation = (context && context.navigation) || [];
            env.addGlobal('callora_navigation', function () {
                return navigation;
            });
        }
        """;

    private readonly ISurfaceTemplateBundleProvider? _bundleProvider;

    public NunjucksSurfaceRenderer()
    {
    }

    /// <summary>Enables <c>@bundle/path</c> includes/extends against the given provider.</summary>
    public NunjucksSurfaceRenderer(ISurfaceTemplateBundleProvider bundleProvider)
    {
        ArgumentNullException.ThrowIfNull(bundleProvider);
        _bundleProvider = bundleProvider;
    }

    public string Render(string templateText, SurfaceRenderContext context) =>
        RenderCore(templateText, context, loader: null);

    public string Render(string templateText, SurfaceRenderContext context, IReadOnlyList<string> bundleChain)
    {
        ArgumentNullException.ThrowIfNull(bundleChain);
        var loader = _bundleProvider is not null && bundleChain.Count > 0
            ? new BundleFileLoader(_bundleProvider, bundleChain)
            : null;
        return RenderCore(templateText, context, loader);
    }

    private static string RenderCore(string templateText, SurfaceRenderContext context, BundleFileLoader? loader)
    {
        ArgumentNullException.ThrowIfNull(templateText);
        ArgumentNullException.ThrowIfNull(context);

        // A fresh, CLR-denied engine per render — no AllowClr(), so JS cannot reach
        // any .NET type; only the two values we set below are visible.
        var engine = new Engine(options => options
            .LimitRecursion(RecursionLimit)
            .TimeoutInterval(TimeSpan.FromSeconds(TimeoutSeconds))
            .LimitMemory(MemoryLimitBytes)
            .MaxStatements(MaxStatements));

        try
        {
            engine.Execute(RenderHarness);
            engine.Execute(NunjucksSource);
            engine.Execute(CompositionScript);

            if (loader is not null)
            {
                engine.SetValue("__loadTemplate", new Func<string, string?>(loader.TryLoad));
            }
            engine.SetValue("__templateText", templateText);
            engine.SetValue("__contextJson", SerializeContext(context));

            var result = engine.Evaluate(RenderScript);
            var html = result.IsNull() || result.IsUndefined() ? string.Empty : result.AsString();

            if (html.Length > MaxOutputChars)
            {
                throw new SurfaceTemplateException($"Surface template output exceeded {MaxOutputChars} characters.");
            }

            return html;
        }
        catch (JavaScriptException ex)
        {
            throw new SurfaceTemplateException("Surface template render error: " + ex.Message, ex);
        }
        catch (TimeoutException ex)
        {
            throw new SurfaceTemplateException("Surface template render timed out.", ex);
        }
        catch (MemoryLimitExceededException ex)
        {
            throw new SurfaceTemplateException("Surface template exceeded the memory limit.", ex);
        }
        catch (StatementsCountOverflowException ex)
        {
            throw new SurfaceTemplateException("Surface template exceeded the statement limit.", ex);
        }
        catch (RecursionDepthOverflowException ex)
        {
            throw new SurfaceTemplateException("Surface template exceeded the recursion limit.", ex);
        }
    }

    private static string SerializeContext(SurfaceRenderContext context)
    {
        // Only the allowlisted string values reach the template — a JSON document,
        // never a .NET object graph.
        var model = new
        {
            workspace = new { key = context.WorkspaceKey },
            surface = new { key = context.SurfaceKey, type = context.SurfaceType },
            tenant = new { key = context.TenantKey },
            locale = context.Locale,
            tokens = context.Tokens,
            slots = context.Slots,
            navigation = context.Navigation,
            caller = context.Caller is null
                ? null
                : new
                {
                    state = context.Caller.State,
                    issuer = context.Caller.Issuer,
                    subjectId = context.Caller.SubjectId,
                    displayName = context.Caller.DisplayName,
                    claims = context.Caller.Claims,
                    claimsJson = context.Caller.ClaimsJson,
                },
        };
        return JsonSerializer.Serialize(model, ContextSerializerOptions);
    }

    private static string LoadNunjucksSource()
    {
        var assembly = typeof(NunjucksSurfaceRenderer).Assembly;
        using var stream = assembly.GetManifestResourceStream("Callora.Surface.Rendering.Resources.nunjucks.js")
            ?? throw new InvalidOperationException("Embedded Nunjucks bundle not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
