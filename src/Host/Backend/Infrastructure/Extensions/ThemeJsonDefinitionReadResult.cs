using Callora.Host.Backend.Application.Extensions;

namespace Callora.Host.Backend.Infrastructure.Extensions;

public sealed record ThemeJsonDefinitionReadResult(
    bool ShouldReplaceDefinitions,
    IReadOnlyList<WorkspaceTemplateDefinitionInput> Definitions);
