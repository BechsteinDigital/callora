using Callora.Host.Backend.Domain.Extensions;

namespace Callora.Host.Backend.Application.Abstractions.Plugins;

public sealed record PluginPackageExtensionRegistration(
    string ExtensionPointId,
    ExtensionSurface Surface);
