using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Workspaces;
using Microsoft.Extensions.Logging;

namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Answers "who is calling this surface" for one request (ADR-017 §5, §6).
/// <para>
/// The order is fixed and deliberate. An assigned plugin provider always wins; the
/// host source only fills in when no binding exists at all. An assigned binding the
/// host cannot honour — plugin unavailable, no provider exported, provider failing —
/// does <em>not</em> degrade to the host source or to anonymous: a missing theme falls
/// back to the base theme because that is cosmetic, while a missing identity provider
/// would be an access leak.
/// </para>
/// </summary>
public sealed class SurfaceIdentityResolver
{
    private readonly IPluginAvailabilityEvaluator _availabilityEvaluator;
    private readonly ICalloraPluginCatalog _pluginCatalog;
    private readonly ISurfaceHostIdentitySource _hostIdentitySource;
    private readonly SurfaceIdentityOptions _options;
    private readonly SurfaceIdentityNormalizer _normalizer;
    private readonly ILogger<SurfaceIdentityResolver> _logger;

    /// <summary>
    /// Creates the resolver.
    /// </summary>
    /// <param name="availabilityEvaluator">Decides whether the assigned plugin is available in the workspace.</param>
    /// <param name="pluginCatalog">Source of exported identity providers.</param>
    /// <param name="hostIdentitySource">Fallback source used only when no provider is bound.</param>
    /// <param name="options">Host bounds on identity shape, lifetime and the provider deadline.</param>
    /// <param name="timeProvider">Clock used for expiry and skew checks.</param>
    /// <param name="logger">Diagnostics for provider failures.</param>
    public SurfaceIdentityResolver(
        IPluginAvailabilityEvaluator availabilityEvaluator,
        ICalloraPluginCatalog pluginCatalog,
        ISurfaceHostIdentitySource hostIdentitySource,
        SurfaceIdentityOptions options,
        TimeProvider timeProvider,
        ILogger<SurfaceIdentityResolver> logger)
    {
        ArgumentNullException.ThrowIfNull(availabilityEvaluator);
        ArgumentNullException.ThrowIfNull(pluginCatalog);
        ArgumentNullException.ThrowIfNull(hostIdentitySource);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _availabilityEvaluator = availabilityEvaluator;
        _pluginCatalog = pluginCatalog;
        _hostIdentitySource = hostIdentitySource;
        _options = options;
        _normalizer = new SurfaceIdentityNormalizer(
            options,
            new SurfaceIdentityClaimNormalizer(options),
            timeProvider);
        _logger = logger;
    }

    /// <summary>
    /// Resolves the identity for one request against the surface's binding.
    /// </summary>
    /// <param name="surface">The resolved surface, carrying its identity assignment.</param>
    /// <param name="request">Transport-neutral facts about the request.</param>
    /// <param name="credentials">Reader for the provider's declared credential sources.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<SurfaceIdentityResolution> ResolveAsync(
        WorkspaceSurfaceSnapshot surface,
        SurfaceRequestDescriptor request,
        ISurfaceCredentialReader credentials,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(credentials);

        return string.IsNullOrWhiteSpace(surface.IdentityPluginId)
            ? await ResolveFromHostAsync(surface, request, cancellationToken).ConfigureAwait(false)
            : await ResolveFromPluginAsync(
                    surface,
                    surface.IdentityPluginId.Trim(),
                    request,
                    credentials,
                    cancellationToken)
                .ConfigureAwait(false);
    }

    private async Task<SurfaceIdentityResolution> ResolveFromHostAsync(
        WorkspaceSurfaceSnapshot surface,
        SurfaceRequestDescriptor request,
        CancellationToken cancellationToken)
    {
        var candidate = await _hostIdentitySource
            .AuthenticateAsync(BuildRequest(surface, request, []), surface.Authentication, cancellationToken)
            .ConfigureAwait(false);
        if (!candidate.IsIdentified)
        {
            // No binding and no principal: this is not a misconfiguration, it is an
            // ordinary anonymous visit. Whether the surface may serve it is the access
            // mode's decision, not this one's.
            return SurfaceIdentityResolution.Anonymous;
        }

        // Only the host source may issue under the reserved callora. namespace.
        var normalization = _normalizer.Normalize(candidate, allowReservedIssuer: true);
        if (normalization.Caller is null)
        {
            _logger.LogError(
                "Host identity source produced an invalid identity for surface {SurfaceKey} in workspace {WorkspaceKey}: {Reason} ({Detail}).",
                surface.SurfaceKey,
                surface.WorkspaceKey,
                normalization.Reason,
                normalization.Detail);
            return SurfaceIdentityResolution.Closed(
                SurfaceIdentityResolutionStatus.ProviderFailed,
                normalization.Detail);
        }

        return SurfaceIdentityResolution.Authenticated(normalization.Caller);
    }

    private async Task<SurfaceIdentityResolution> ResolveFromPluginAsync(
        WorkspaceSurfaceSnapshot surface,
        string pluginId,
        SurfaceRequestDescriptor request,
        ISurfaceCredentialReader credentials,
        CancellationToken cancellationToken)
    {
        // Availability belongs in this check, not next to it: deactivated, unentitled,
        // unhealthy or removed all mean the same thing here — there is no provider.
        var availability = await _availabilityEvaluator
            .EvaluateAsync(pluginId, surface.WorkspaceKey, cancellationToken)
            .ConfigureAwait(false);
        if (!availability.IsAvailable)
        {
            return SurfaceIdentityResolution.Closed(
                SurfaceIdentityResolutionStatus.ProviderUnavailable,
                $"Plugin '{pluginId}' is not available in workspace '{surface.WorkspaceKey}': "
                + string.Join(", ", availability.UnmetFactors));
        }

        if (FindProvider(pluginId) is not { } provider)
        {
            return SurfaceIdentityResolution.Closed(
                SurfaceIdentityResolutionStatus.ProviderMissing,
                $"Plugin '{pluginId}' exports no {nameof(IHostSurfaceIdentityProvider)}.");
        }

        var candidate = await InvokeAsync(provider, surface, request, credentials, cancellationToken)
            .ConfigureAwait(false);
        if (candidate is null)
        {
            return SurfaceIdentityResolution.Closed(
                SurfaceIdentityResolutionStatus.ProviderFailed,
                $"Identity provider of plugin '{pluginId}' failed or exceeded its deadline.");
        }

        if (!candidate.IsIdentified)
        {
            return SurfaceIdentityResolution.Anonymous;
        }

        var normalization = _normalizer.Normalize(candidate);
        if (normalization.Caller is null)
        {
            _logger.LogWarning(
                "Identity provider of plugin {PluginId} returned an unusable identity for surface {SurfaceKey}: {Reason} ({Detail}).",
                pluginId,
                surface.SurfaceKey,
                normalization.Reason,
                normalization.Detail);
            return SurfaceIdentityResolution.Closed(
                SurfaceIdentityResolutionStatus.ProviderFailed,
                normalization.Detail);
        }

        return SurfaceIdentityResolution.Authenticated(normalization.Caller);
    }

    private IHostSurfaceIdentityProvider? FindProvider(string pluginId)
    {
        foreach (var export in _pluginCatalog.GetOwnedExports(typeof(IHostSurfaceIdentityProvider)))
        {
            if (export.Service is IHostSurfaceIdentityProvider provider &&
                string.Equals(export.PluginId, pluginId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(provider.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
            {
                return provider;
            }
        }

        return null;
    }

    private async Task<HostSurfaceIdentityResult?> InvokeAsync(
        IHostSurfaceIdentityProvider provider,
        WorkspaceSurfaceSnapshot surface,
        SurfaceRequestDescriptor request,
        ISurfaceCredentialReader credentials,
        CancellationToken cancellationToken)
    {
        var payload = BuildRequest(surface, request, ReadCredentials(provider, credentials));

        // A slow provider delays every render of its surface, so the wait is bounded.
        // The timeout is a provider failure, never a quiet fall-through to anonymous.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_options.ProviderTimeout);

        try
        {
            return await provider.AuthenticateAsync(payload, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Identity provider of plugin {PluginId} exceeded its {Timeout} deadline for surface {SurfaceKey}.",
                provider.PluginId,
                _options.ProviderTimeout,
                surface.SurfaceKey);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Identity provider of plugin {PluginId} threw while authenticating surface {SurfaceKey}.",
                provider.PluginId,
                surface.SurfaceKey);
            return null;
        }
    }

    private static IReadOnlyList<SurfaceIdentityCredential> ReadCredentials(
        IHostSurfaceIdentityProvider provider,
        ISurfaceCredentialReader reader)
    {
        var sources = provider.CredentialSources;
        if (sources is null || sources.Count == 0)
        {
            return [];
        }

        var credentials = new List<SurfaceIdentityCredential>(sources.Count);
        foreach (var source in sources)
        {
            if (source is null || string.IsNullOrWhiteSpace(source.Name))
            {
                continue;
            }

            // A declared source the request does not carry stays absent rather than
            // arriving as an empty string, so a provider can tell the two apart.
            if (reader.Read(source.Kind, source.Name) is { } value)
            {
                credentials.Add(new SurfaceIdentityCredential(source.Kind, source.Name, value));
            }
        }

        return credentials;
    }

    private static HostSurfaceIdentityRequest BuildRequest(
        WorkspaceSurfaceSnapshot surface,
        SurfaceRequestDescriptor request,
        IReadOnlyList<SurfaceIdentityCredential> credentials) =>
        new(
            surface.TenantKey,
            surface.WorkspaceKey,
            surface.SurfaceKey,
            request.HttpMethod,
            request.RoutePath,
            request.Locale,
            credentials,
            request.Origin,
            request.UserAgent);
}
