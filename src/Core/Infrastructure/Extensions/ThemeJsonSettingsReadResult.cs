using Callora.Core.Application.Extensions;

namespace Callora.Core.Infrastructure.Extensions;

public sealed record ThemeJsonSettingsReadResult(
    bool ShouldReplaceDefinitions,
    IReadOnlyList<WorkspaceThemeSettingDefinitionInput> Definitions);
