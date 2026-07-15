namespace Callora.Core.Application.Jobs.Contracts;

/// <summary>
/// Enqueue request for one background job.
/// </summary>
/// <param name="JobType">Handler routing key, for example "dialer.run".</param>
/// <param name="PayloadJson">Raw JSON payload passed to the handler.</param>
/// <param name="RunAtUtc">Earliest execution time; null runs as soon as possible.</param>
/// <param name="MaxAttempts">Total attempts including the first one.</param>
/// <param name="WorkspaceKey">Optional workspace scope forwarded to the handler.</param>
public sealed record BackgroundJobRequest(
    string JobType,
    string PayloadJson,
    DateTimeOffset? RunAtUtc = null,
    int MaxAttempts = 3,
    string? WorkspaceKey = null);
