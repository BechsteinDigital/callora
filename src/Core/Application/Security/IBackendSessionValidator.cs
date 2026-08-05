using System.Security.Claims;

namespace Callora.Core.Application.Security;

/// <summary>
/// Decides whether an already-signature-valid session may still act (#105). Runs on
/// the request hot path, so implementations must be bounded — a short-lived cache
/// over the account lookup, never an unbounded per-request query fan-out.
/// </summary>
public interface IBackendSessionValidator
{
    /// <summary>
    /// Validates the principal behind a presented token. Returns null when the
    /// session is still valid, otherwise a short reason for the rejection (logged,
    /// never returned to the caller — the response stays a bare 401).
    /// </summary>
    Task<string?> ValidateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}
