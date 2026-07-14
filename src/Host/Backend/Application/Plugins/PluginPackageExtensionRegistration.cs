using Callora.Host.Backend.Domain.Extensions;

namespace Callora.Host.Backend.Application.Plugins;

public sealed record PluginPackageExtensionRegistration(
    string ExtensionPointId,
    ExtensionSurface Surface);
