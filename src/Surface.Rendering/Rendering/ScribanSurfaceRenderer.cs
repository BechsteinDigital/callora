using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Callora.Surface.Rendering.Rendering;

/// <summary>
/// The bought base engine (ADR-015 A) in a hardened sandbox: the model is built
/// purely from strings and <see cref="ScriptObject"/>s and ALL CLR member access
/// is denied, so a (possibly untrusted) template can never reflect into a .NET
/// type. Loop/recursion limits and an output cap bound template DoS (§8).
/// </summary>
public sealed class ScribanSurfaceRenderer : ISurfaceRenderer
{
    private const int TemplateLoopLimit = 1000;
    private const int TemplateRecursiveLimit = 50;
    private const int MaxOutputChars = 512 * 1024;

    private readonly ISurfaceTemplateBundleProvider? _bundleProvider;

    public ScribanSurfaceRenderer()
    {
    }

    /// <summary>Enables <c>@bundle/path</c> includes against the given provider.</summary>
    public ScribanSurfaceRenderer(ISurfaceTemplateBundleProvider bundleProvider)
    {
        ArgumentNullException.ThrowIfNull(bundleProvider);
        _bundleProvider = bundleProvider;
    }

    public string Render(string templateText, SurfaceRenderContext context) =>
        RenderCore(templateText, context, templateLoader: null);

    public string Render(string templateText, SurfaceRenderContext context, IReadOnlyList<string> bundleChain)
    {
        ArgumentNullException.ThrowIfNull(bundleChain);
        // Includes stay off unless a provider is configured AND the surface resolved
        // to a bundle chain — otherwise there is no in-scope bundle to load from.
        var loader = _bundleProvider is not null && bundleChain.Count > 0
            ? new BundleTemplateLoader(_bundleProvider, bundleChain)
            : null;
        return RenderCore(templateText, context, loader);
    }

    private static string RenderCore(string templateText, SurfaceRenderContext context, ITemplateLoader? templateLoader)
    {
        ArgumentNullException.ThrowIfNull(templateText);
        ArgumentNullException.ThrowIfNull(context);

        var template = Template.Parse(templateText);
        if (template.HasErrors)
        {
            throw new SurfaceTemplateException(
                "Surface template parse error: " +
                string.Join("; ", template.Messages.Select(static m => m.Message)));
        }

        var templateContext = new TemplateContext
        {
            StrictVariables = false,
            EnableRelaxedMemberAccess = false,
            LoopLimit = TemplateLoopLimit,
            RecursiveLimit = TemplateRecursiveLimit,
            // Deny every CLR member: the model below is strings + ScriptObjects, so
            // nothing legitimate needs reflection and nothing can reach a .NET type.
            MemberFilter = static _ => false,
            // Absent a loader, include/import have no source at all (E1 default).
            TemplateLoader = templateLoader,
        };
        templateContext.PushGlobal(BuildAllowlistedModel(context));

        string html;
        try
        {
            html = template.Render(templateContext);
        }
        catch (ScriptRuntimeException ex)
        {
            throw new SurfaceTemplateException("Surface template render error: " + ex.Message, ex);
        }

        if (html.Length > MaxOutputChars)
        {
            throw new SurfaceTemplateException(
                $"Surface template output exceeded {MaxOutputChars} characters.");
        }

        return html;
    }

    private static ScriptObject BuildAllowlistedModel(SurfaceRenderContext context)
    {
        var tokens = new ScriptObject();
        foreach (var pair in context.Tokens)
        {
            tokens[pair.Key] = pair.Value;
        }

        return new ScriptObject
        {
            ["workspace"] = new ScriptObject { ["key"] = context.WorkspaceKey },
            ["surface"] = new ScriptObject { ["key"] = context.SurfaceKey, ["type"] = context.SurfaceType },
            ["tenant"] = new ScriptObject { ["key"] = context.TenantKey },
            ["locale"] = context.Locale,
            ["tokens"] = tokens,
        };
    }
}
