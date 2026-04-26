using Callora.Host.Backend.Application.Abstractions.Extensions;

namespace Callora.Host.Backend.Infrastructure.Extensions;

public sealed record ThemeJsonDefinitionReadResult(
    bool ShouldReplaceDefinitions,
    IReadOnlyList<WorkspaceTemplateDefinitionInput> Definitions);
