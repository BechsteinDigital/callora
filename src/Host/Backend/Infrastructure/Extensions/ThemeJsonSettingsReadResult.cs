using Callora.Host.Backend.Application.Extensions;

namespace Callora.Host.Backend.Infrastructure.Extensions;

public sealed record ThemeJsonSettingsReadResult(
    bool ShouldReplaceDefinitions,
    IReadOnlyList<WorkspaceThemeSettingDefinitionInput> Definitions);
