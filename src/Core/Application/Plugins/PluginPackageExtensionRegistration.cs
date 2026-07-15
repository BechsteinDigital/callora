using Callora.Core.Domain.Extensions;

namespace Callora.Core.Application.Plugins;

public sealed record PluginPackageExtensionRegistration(
    string ExtensionPointId,
    ExtensionSurface Surface);
