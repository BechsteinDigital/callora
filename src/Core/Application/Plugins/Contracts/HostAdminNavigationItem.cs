namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// One Admin UI navigation entry contributed by a plugin.
/// </summary>
/// <param name="Id">Stable entry identifier.</param>
/// <param name="Label">Display label.</param>
/// <param name="To">Target client route.</param>
/// <param name="Icon">Optional icon identifier.</param>
/// <param name="Order">Sort order (ascending).</param>
/// <param name="RequiredPermission">Optional permission required to show this entry.</param>
public sealed record HostAdminNavigationItem(
    string Id,
    string Label,
    string To,
    string? Icon = null,
    int Order = 100,
    string? RequiredPermission = null);
