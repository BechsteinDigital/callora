using System.Text.Json;
using Callora.Core.Application.Surfaces;

namespace Callora.Surface.Rendering;

/// <summary>
/// Projects a <see cref="SurfaceCaller"/> onto the allowlisted values a template and
/// the browser runtime may see. The projection is where the guest/authenticated
/// distinction becomes a string — so it is also the last place that has to get it
/// right: a guest never gains a display name or claims on the way out.
/// </summary>
public static class SurfaceCallerViewFactory
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> NoClaims =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    /// <summary>Builds the view for one caller.</summary>
    /// <param name="caller">The established caller.</param>
    public static SurfaceCallerView Create(SurfaceCaller caller)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return caller is AuthenticatedSurfaceCaller authenticated
            ? new SurfaceCallerView(
                SurfaceCallerView.AuthenticatedState,
                authenticated.Subject.Issuer,
                authenticated.Subject.SubjectId,
                authenticated.Identity.DisplayName,
                authenticated.Identity.Claims,
                JsonSerializer.Serialize(
                    authenticated.Identity.Claims.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal),
                    SerializerOptions))
            : new SurfaceCallerView(
                SurfaceCallerView.GuestState,
                caller.Subject.Issuer,
                caller.Subject.SubjectId,
                string.Empty,
                NoClaims,
                "{}");
    }
}
