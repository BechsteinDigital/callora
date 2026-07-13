namespace Callora.Host.PluginContracts.Application.Http;

/// <summary>
/// Problem payload of a failed controller action; the host renders it as
/// RFC 9457 application/problem+json with the configured type base URI.
/// </summary>
/// <param name="Status">HTTP status code.</param>
/// <param name="Title">Short human-readable summary.</param>
/// <param name="Detail">Explanation specific to this occurrence.</param>
public sealed record ApiProblemPayload(int Status, string Title, string Detail);
