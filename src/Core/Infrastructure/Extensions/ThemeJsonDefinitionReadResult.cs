using Callora.Core.Application.Extensions;

namespace Callora.Core.Infrastructure.Extensions;

public sealed record ThemeJsonDefinitionReadResult(
    bool ShouldReplaceDefinitions,
    IReadOnlyList<WorkspaceTemplateDefinitionInput> Definitions);
