namespace Callora.Plugin.Composer.Application.Admin;

/// <summary>What publishing sends. The label is optional and names the publication in the history.</summary>
public sealed record LayoutPublishRequest(string? Label);
