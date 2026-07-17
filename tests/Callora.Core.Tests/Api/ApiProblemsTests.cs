using Callora.Core.Api;
using Callora.Core.Application.Webhooks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Callora.Core.Tests.Api;

public sealed class ApiProblemsTests
{
    [Fact]
    public void NotFound_ProducesRfc9457ShapeWithConfigurableType()
    {
        var result = ApiProblems.NotFound("Workspace 'x' not found.");

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.ProblemDetails.Status);
        Assert.Equal("Not Found", problem.ProblemDetails.Title);
        Assert.Equal("Workspace 'x' not found.", problem.ProblemDetails.Detail);
        Assert.StartsWith("urn:callora:problem:", problem.ProblemDetails.Type, StringComparison.Ordinal);
        Assert.EndsWith("not-found", problem.ProblemDetails.Type, StringComparison.Ordinal);
    }

    [Fact]
    public void BadRequestAndConflict_CarryMatchingStatusCodes()
    {
        var badRequest = Assert.IsType<ProblemHttpResult>(ApiProblems.BadRequest("nope"));
        var conflict = Assert.IsType<ProblemHttpResult>(ApiProblems.Conflict("busy"));

        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.ProblemDetails.Status);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.ProblemDetails.Status);
    }

    [Fact]
    public void FromException_CarriesStatusTypeAndCode()
    {
        var result = ApiProblems.FromException(WebhookTargetException.Blocked("evil.example"));

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.ProblemDetails.Status);
        Assert.Equal("WEBHOOK__TARGET_BLOCKED", problem.ProblemDetails.Extensions["code"]);
        Assert.EndsWith("WEBHOOK__TARGET_BLOCKED", problem.ProblemDetails.Type, StringComparison.Ordinal);
        Assert.Contains("evil.example", problem.ProblemDetails.Detail!, StringComparison.Ordinal);
    }
}
