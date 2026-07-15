using Callora.Core.Application.Policies;
using Callora.Core.Infrastructure.Security;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Callora.Core.Api.OpenApi;

/// <summary>
/// Registers the API-key security scheme (the configured header) on the OpenAPI
/// document and requires it on every operation, so the reference UI offers an
/// authorize control. Replaces the former Swashbuckle security definition.
/// </summary>
internal sealed class ApiKeySecuritySchemeTransformer(BackendHostOptions options) : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var schemeId = ApiKeyAuthenticationDefaults.Scheme;

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[schemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            Name = options.ApiKeyHeaderName,
            In = ParameterLocation.Header,
            Description = $"API key required in header '{options.ApiKeyHeaderName}'."
        };

        if (document.Paths is null)
            return Task.CompletedTask;

        foreach (var pathItem in document.Paths.Values)
        {
            if (pathItem.Operations is null)
                continue;

            foreach (var operation in pathItem.Operations.Values)
            {
                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(schemeId, document)] = []
                });
            }
        }

        return Task.CompletedTask;
    }
}
